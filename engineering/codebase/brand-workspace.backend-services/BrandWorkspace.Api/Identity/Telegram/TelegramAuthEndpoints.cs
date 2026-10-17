using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Telegram.Bot;
using Telegram.Bot.Types;
using WoW.Two.Sdk.Beta.Be.Identity.Otp;
using WoW.Two.Sdk.Beta.Be.Identity.Otp.Models;

namespace BrandWorkspace.Api.Identity.Telegram;

/// <summary>Exposes bot-based sign-in while preserving the operator session and explicit account-link boundary.</summary>
public static class TelegramAuthEndpoints
{
    public const string Scope = "ocharo-desk-sign-in";
    public const string RatePolicy = "telegram-auth";

    public static WebApplication MapTelegramAuthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/auth/telegram", MetadataAsync).AllowAnonymous();
        app.MapPost("/api/auth/telegram/link", LinkAsync).RequireRateLimiting(RatePolicy);
        app.MapDelete("/api/auth/telegram/link", UnlinkAsync).RequireRateLimiting(RatePolicy);
        app.MapPost("/api/auth/telegram/code", SendCodeAsync).AllowAnonymous().RequireRateLimiting(RatePolicy);
        app.MapPost("/api/auth/telegram/login", LoginAsync).AllowAnonymous().RequireRateLimiting(RatePolicy);
        app.MapPost("/api/auth/telegram/updates", UpdateAsync).AllowAnonymous()
            .WithMetadata(new SkipAntiforgeryValidation(), new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(65_536));
        return app;
    }

    private static async Task<IResult> MetadataAsync(HttpContext http, TelegramAuthSettings settings, TelegramAuthRepository repository)
    {
        var link = settings.Enabled && http.User.Identity?.IsAuthenticated == true
            ? await repository.FindByUserAsync(AccessClaims.UserId(http.User), http.RequestAborted) : null;
        return Results.Ok(new { data = new { enabled = settings.Enabled,
            botUsername = settings.Enabled ? settings.BotUsername : null, linked = link is not null, phone = link?.Phone } });
    }

    private static async Task<IResult> LinkAsync(PasswordInput input, HttpContext http, TelegramAuthSettings settings,
        UserManager<AccessUser> users, SignInManager<AccessUser> signIn, TelegramAuthRepository repository, TimeProvider clock)
    {
        if (!settings.Enabled) return Unavailable();
        var user = await ReauthenticateAsync(input.Password, http, users, signIn);
        if (user is null) return AccessProblems.Create(401, "The password is incorrect or this account cannot sign in", "invalid_credentials");
        await repository.BeginAsync("telegram-link", http.RequestAborted);
        if (await repository.FindByUserAsync(user.Id, http.RequestAborted) is not null)
            return AccessProblems.Create(409, "Unlink the existing Telegram account before linking another", "telegram_already_linked");
        var secret = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expires = clock.GetUtcNow().AddMinutes(10);
        await repository.CreateLinkRequestAsync(Hash(secret), user.Id, AccessClaims.Session(http.User)!.Value, expires, http.RequestAborted);
        await repository.CommitAsync(http.RequestAborted);
        return Results.Ok(new { data = new { botUrl = $"https://t.me/{settings.BotUsername}?start=link_{secret}", expiresAt = expires } });
    }

    private static async Task<IResult> UnlinkAsync([Microsoft.AspNetCore.Mvc.FromBody] PasswordInput input, HttpContext http, TelegramAuthSettings settings,
        UserManager<AccessUser> users, SignInManager<AccessUser> signIn, TelegramAuthRepository repository)
    {
        if (!settings.Enabled) return Unavailable();
        var user = await ReauthenticateAsync(input.Password, http, users, signIn);
        if (user is null) return AccessProblems.Create(401, "The password is incorrect or this account cannot sign in", "invalid_credentials");
        await repository.BeginAsync("telegram-link", http.RequestAborted);
        await repository.RemoveLinkAndSessionsAsync(user.Id, http.RequestAborted);
        await repository.CommitAsync(http.RequestAborted);
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> SendCodeAsync(PhoneInput input, HttpContext http, TelegramAuthSettings settings,
        TelegramAuthRepository repository, UserManager<AccessUser> users, IOtpService otp,
        IServiceProvider services, TimeProvider clock)
    {
        if (!settings.Enabled) return Unavailable();
        var phone = NormalizePhone(input.Phone);
        if (phone is null) return AccessProblems.Create(400, "Use an international phone number", "invalid_phone");
        var expires = clock.GetUtcNow().AddMinutes(5);
        await repository.BeginAsync("telegram-code:" + phone, http.RequestAborted);
        var link = await repository.FindByPhoneAsync(phone, http.RequestAborted);
        var user = link is null ? null : await users.FindByIdAsync(link.UserId);
        if (await CanSignInAsync(user, users))
        {
            var code = await otp.CreateAsync(phone, Scope, http.RequestAborted);
            if (code.Success)
            {
                var delivery = services.GetRequiredKeyedService<IOtpDeliveryHandler>(OtpChannelNameConstants.Telegram);
                using var deliveryTimeout = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted);
                deliveryTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                var sent = false;
                try
                {
                    var result = await delivery.SendAsync(new OtpDeliveryEnvelopeModel
                {
                    DeliveryAddress = link!.TelegramId.ToString(CultureInfo.InvariantCulture), Code = code.Code!, Scope = Scope,
                    Text = $"Ocharo sign-in code: {code.Code}\nExpires in 5 minutes. Enter it only in Ocharo. Never share it with anyone."
                }, deliveryTimeout.Token);
                    sent = result.Success;
                }
                catch (OperationCanceledException) when (!http.RequestAborted.IsCancellationRequested) { }
                catch (HttpRequestException) { }
                if (!sent) await repository.ConsumePendingAsync(phone, Scope, http.RequestAborted);
            }
        }
        await repository.CommitAsync(http.RequestAborted);
        return Results.Accepted(value: new { data = new { expiresAt = expires } });
    }

    private static async Task<IResult> LoginAsync(CodeInput input, HttpContext http, TelegramAuthSettings settings,
        TelegramAuthRepository repository, UserManager<AccessUser> users, IOtpService otp, TimeProvider clock)
    {
        if (!settings.Enabled) return Unavailable();
        var phone = NormalizePhone(input.Phone);
        if (phone is null || input.Code is null || !Regex.IsMatch(input.Code, "^[0-9]{6}$", RegexOptions.CultureInvariant)) return InvalidCode();
        await repository.BeginAsync("telegram-code:" + phone, http.RequestAborted);
        var verification = await otp.VerifyAsync(phone, input.Code, Scope, http.RequestAborted);
        var link = await repository.FindByPhoneAsync(phone, http.RequestAborted);
        var user = link is null ? null : await users.FindByIdAsync(link.UserId);
        if (!verification.Success || !await CanSignInAsync(user, users))
        {
            await repository.CommitAsync(http.RequestAborted);
            return InvalidCode();
        }
        var ticket = AccessSessionIssuer.Create(user!, clock.GetUtcNow());
        if (!await repository.CreateSessionAsync(ticket.Id, user!, ticket.ExpiresAt, AccessClaims.Session(http.User), http.RequestAborted))
        {
            await repository.CommitAsync(http.RequestAborted);
            return InvalidCode();
        }
        await repository.CommitAsync(http.RequestAborted);
        await AccessSessionIssuer.WriteCookieAsync(http, user!, ticket);
        return Results.Ok(new { data = new { authenticated = true } });
    }

    private static async Task<IResult> UpdateAsync(HttpContext http, TelegramAuthSettings settings, IServiceProvider services)
    {
        if (!settings.Enabled || settings.PollingEnabled) return Unavailable();
        var supplied = http.Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        if (string.IsNullOrEmpty(supplied) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(settings.WebhookSecret)))
            return AccessProblems.Create(401, "Telegram webhook authentication is required", "telegram_webhook_unauthorized");
        if (http.Request.ContentLength > 65_536)
            return AccessProblems.Create(413, "Telegram update is too large", "telegram_update_too_large");
        if (http.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size)
            size.MaxRequestBodySize = 65_536;
        Update? update;
        try { update = await JsonSerializer.DeserializeAsync<Update>(http.Request.Body, JsonBotAPI.Options, http.RequestAborted); }
        catch (JsonException) { return AccessProblems.Create(400, "Invalid Telegram update", "telegram_update_invalid"); }
        if (update is not null) await services.GetRequiredService<TelegramBotUpdatesHandler>().HandleAsync(update, http.RequestAborted);
        return Results.Ok();
    }

    private static async Task<AccessUser?> ReauthenticateAsync(string? password, HttpContext http,
        UserManager<AccessUser> users, SignInManager<AccessUser> signIn)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 1024) return null;
        var user = await users.FindByIdAsync(AccessClaims.UserId(http.User));
        if (!await CanSignInAsync(user, users)) return null;
        return (await signIn.CheckPasswordSignInAsync(user!, password, lockoutOnFailure: true)).Succeeded ? user : null;
    }

    private static async Task<bool> CanSignInAsync(AccessUser? user, UserManager<AccessUser> users)
    {
        return user is { IsActive: true, TwoFactorEnabled: false } && !await users.IsLockedOutAsync(user);
    }

    public static string? NormalizePhone(string? input)
    {
        if (input is null || input.Length > 40) return null;
        var phone = input.Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal)
            .Replace("(", "", StringComparison.Ordinal).Replace(")", "", StringComparison.Ordinal);
        if (!phone.StartsWith('+')) phone = "+" + phone;
        return Regex.IsMatch(phone, "^\\+[1-9][0-9]{6,14}$", RegexOptions.CultureInvariant) ? phone : null;
    }

    public static string Hash(string secret)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    private static IResult Unavailable()
    {
        return AccessProblems.Create(404, "Telegram sign-in is not configured", "telegram_unavailable");
    }

    private static IResult InvalidCode()
    {
        return AccessProblems.Create(401, "The code is incorrect or expired. Request another code.", "telegram_code_invalid");
    }

    public sealed record PasswordInput(string? Password);
    public sealed record PhoneInput(string? Phone);
    public sealed record CodeInput(string? Phone, string? Code);
}
