using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using BrandWorkspace.Api;
using BrandWorkspace.Api.Identity.Telegram;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Telegram.Bot;
using WoW.Two.Sdk.Beta.Be.Testing;
using Xunit;

namespace BrandWorkspace.Tests.E2E.Access;

public sealed class TelegramAuthTests(AccessDatabaseFixture database) : IClassFixture<AccessDatabaseFixture>
{
    private const string Password = "Test-only-Correct#927";
    private const string Secret = "synthetic-webhook-secret-827826409-safe";
    private const string Phone = "+998901234567";
    private const long Chat = 718264009;

    [Fact]
    public async Task Disabled_ShouldPreservePasswordLoginAndRejectTelegramWithoutSecrets()
    {
        await using var factory = database.CreateFactory();
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var metadata = await DataAsync(await client.GetAsync("/api/auth/telegram"));
        metadata.GetProperty("enabled").GetBoolean().Should().BeFalse();
        var csrf = await CsrfAsync(client);
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone = Phone }, csrf)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var user = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        await AccessDatabaseFixture.LoginAsync(client, user);
        (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("authenticated").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Help_ShouldExplainExplicitAccountConnection()
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        (await UpdateAsync(client, new { update_id = 1, message = Message(Chat, text: "/help") })).StatusCode.Should().Be(HttpStatusCode.OK);
        bot.Messages.Should().ContainSingle().Which.Should().Contain("Connect Telegram");
    }

    [Fact]
    public async Task Linking_ShouldRequirePasswordCsrfPrivateChatOwnContactAndLiveChallenge()
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var user = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        var csrf = await AccessDatabaseFixture.LoginAsync(client, user);
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/link", new { password = Password }, null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/link", new { password = "wrong" }, csrf)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var started = await DataAsync(await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/link", new { password = Password }, csrf));
        var start = new Uri(started.GetProperty("botUrl").GetString()!).Query.Split('=')[1];
        (await UpdateAsync(client, new { update_id = 1, message = Message(Chat, text: "/start " + start) }, secret: "wrong")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await UpdateAsync(client, new { update_id = 2, message = Message(Chat, text: "/start " + start, chatType: "group") });
        await UpdateAsync(client, new { update_id = 3, message = Message(Chat, contactUser: Chat, phone: "+998901234561") });
        (await MetadataAsync(client)).GetProperty("linked").GetBoolean().Should().BeFalse();
        await UpdateAsync(client, new { update_id = 4, message = Message(Chat, text: "/start " + start) });
        await UpdateAsync(client, new { update_id = 5, message = Message(Chat, contactUser: Chat + 1, phone: "+998901234561") });
        (await MetadataAsync(client)).GetProperty("linked").GetBoolean().Should().BeFalse();
        await UpdateAsync(client, new { update_id = 6, message = Message(Chat, contactUser: Chat, phone: "+998901234561") });
        (await MetadataAsync(client)).GetProperty("linked").GetBoolean().Should().BeTrue();
        (await MetadataAsync(client)).GetProperty("phone").GetString().Should().Be("+998901234561");
        await UpdateAsync(client, new { update_id = 7, message = Message(Chat, contactUser: Chat, phone: "+998901234562") });
        (await MetadataAsync(client)).GetProperty("phone").GetString().Should().Be("+998901234561");
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/link", new { password = Password }, csrf)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    public async Task Linking_ShouldRejectAChallengeAfterItsAuthorityEnds(string change)
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var user = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        var csrf = await AccessDatabaseFixture.LoginAsync(client, user);
        var started = await DataAsync(await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/link", new { password = Password }, csrf));
        var start = new Uri(started.GetProperty("botUrl").GetString()!).Query.Split('=')[1];
        if (change == "expired") factory.Clock.Advance(TimeSpan.FromMinutes(11));
        else await database.ExecuteAsync("UPDATE access.sessions SET revoked_at=now() WHERE user_id=$1", user.Id);
        await UpdateAsync(client, new { update_id = 1, message = Message(Chat + 1, text: "/start " + start) });
        await UpdateAsync(client, new { update_id = 2, message = Message(Chat + 1, contactUser: Chat + 1, phone: "+998901234563") });
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<TelegramAuthRepository>().FindByUserAsync(user.Id, default)).Should().BeNull();
    }

    [Fact]
    public async Task Codes_ShouldStayEncryptedAndExpireAfterFiveWrongAttempts()
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var user = await LinkedUserAsync(factory, "+998901234564", Chat + 2);
        var csrf = await CsrfAsync(client);
        await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone = "+998901234564" }, csrf);
        var code = bot.LastCode();
        await using var connection = new NpgsqlConnection(database.Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT protected_code FROM access.telegram_codes WHERE subject=$1 ORDER BY created_at DESC LIMIT 1", connection);
        command.Parameters.AddWithValue("+998901234564");
        ((string)(await command.ExecuteScalarAsync())!).Should().NotBe(code).And.NotContain(code);
        var wrong = code == "000000" ? "111111" : "000000";
        for (var attempt = 0; attempt < 5; attempt++)
            (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/login", new { phone = "+998901234564", code = wrong }, csrf)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/login", new { phone = "+998901234564", code }, csrf)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("authenticated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Codes_ShouldRejectExpiryAndRateLimitRepeatedDelivery()
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        await LinkedUserAsync(factory, "+998901234565", Chat + 3);
        var csrf = await CsrfAsync(client);
        await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone = "+998901234565" }, csrf);
        var code = bot.LastCode();
        var count = bot.Messages.Count;
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone = "+998901234565" }, csrf)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        bot.Messages.Count.Should().Be(count);
        factory.Clock.Advance(TimeSpan.FromMinutes(6));
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/login", new { phone = "+998901234565", code }, csrf)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConcurrentLogin_ShouldConsumeOnceAndUseRevocableDeskSessions()
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var one = AccessDatabaseFixture.CreateClient(factory);
        using var two = AccessDatabaseFixture.CreateClient(factory);
        var user = await LinkedUserAsync(factory, "+998901234566", Chat + 4);
        var csrf1 = await CsrfAsync(one);
        var csrf2 = await CsrfAsync(two);
        await AccessDatabaseFixture.PostAsync(one, "/api/auth/telegram/code", new { phone = "+998901234566" }, csrf1);
        var code = bot.LastCode();
        var responses = await Task.WhenAll(
            AccessDatabaseFixture.PostAsync(one, "/api/auth/telegram/login", new { phone = "+998901234566", code }, csrf1),
            AccessDatabaseFixture.PostAsync(two, "/api/auth/telegram/login", new { phone = "+998901234566", code }, csrf2));
        responses.Select(response => response.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        var winner = responses[0].IsSuccessStatusCode ? one : two;
        var session = await AccessDatabaseFixture.SessionAsync(winner);
        session.GetProperty("user").GetProperty("id").GetString().Should().Be(user.Id);
        var cookie = responses.Single(response => response.IsSuccessStatusCode).Headers.GetValues("Set-Cookie").Single();
        cookie.Should().Contain("__Host-brand-workspace-session=").And.Contain("httponly");
        await database.ExecuteAsync("UPDATE access.sessions SET revoked_at=now() WHERE user_id=$1", user.Id);
        (await winner.GetAsync("/api/organizations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("locked")]
    [InlineData("two-factor")]
    public async Task Codes_ShouldNotBypassAccountRestrictions(string kind)
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var phone = "+998" + Random.Shared.NextInt64(100000000, 999999999);
        var user = await LinkedUserAsync(factory, phone, Random.Shared.NextInt64(800000000, 999999999));
        var csrf = await CsrfAsync(client);
        await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone }, csrf);
        var code = bot.LastCode();
        var sql = kind switch
        {
            "inactive" => "UPDATE access.users SET \"IsActive\"=false WHERE id=$1",
            "locked" => "UPDATE access.users SET \"LockoutEnd\"=now()+interval '1 hour' WHERE id=$1",
            _ => "UPDATE access.users SET \"TwoFactorEnabled\"=true WHERE id=$1"
        };
        await database.ExecuteAsync(sql, user.Id);
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/login", new { phone, code }, csrf)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownPhone_ShouldReturnTheSameAcceptedShapeWithoutSendingAMessage()
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var response = await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone = "+998909999999" }, await CsrfAsync(client));
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await DataAsync(response)).GetProperty("expiresAt").GetDateTimeOffset().Should().BeAfter(factory.Clock.GetUtcNow());
        bot.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Unlink_ShouldRequirePasswordAndRevokeAllSessionsAndPendingCodes()
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var user = await LinkedUserAsync(factory, Phone, Chat + 10);
        var csrf = await AccessDatabaseFixture.LoginAsync(client, user);
        await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone = Phone }, csrf);
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/telegram/link") { Content = JsonContent.Create(new { password = Password }) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("authenticated").GetBoolean().Should().BeFalse();
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/login", new { phone = Phone, code = bot.LastCode() }, await CsrfAsync(client))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Linking_ShouldNotTakeOverAnExistingPhoneOrTelegramIdentity()
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        var first = await LinkedUserAsync(factory, "+998901234568", Chat + 12);
        using var second = AccessDatabaseFixture.CreateClient(factory);
        var secondUser = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        var csrf = await AccessDatabaseFixture.LoginAsync(second, secondUser);
        var started = await DataAsync(await AccessDatabaseFixture.PostAsync(second, "/api/auth/telegram/link", new { password = Password }, csrf));
        var start = new Uri(started.GetProperty("botUrl").GetString()!).Query.Split('=')[1];
        await UpdateAsync(second, new { update_id = 1, message = Message(Chat + 13, text: "/start " + start) });
        await UpdateAsync(second, new { update_id = 2, message = Message(Chat + 13, contactUser: Chat + 13, phone: "+998901234568") });
        (await MetadataAsync(second)).GetProperty("linked").GetBoolean().Should().BeFalse();
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<TelegramAuthRepository>().FindByPhoneAsync("+998901234568", default))!.UserId.Should().Be(first.Id);
    }

    [Fact]
    public async Task DeliveryFailure_ShouldConsumeTheUndeliveredCodeWithoutExposingAccountState()
    {
        var bot = new FakeTelegram { RejectDelivery = true };
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        await LinkedUserAsync(factory, "+998901234569", Chat + 14);
        var csrf = await CsrfAsync(client);
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone = "+998901234569" }, csrf)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/login", new { phone = "+998901234569", code = bot.LastCode() }, csrf)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CodeRequests_ShouldLimitTheRequestingAddress()
    {
        var bot = new FakeTelegram();
        await using var factory = Factory(bot);
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var csrf = await CsrfAsync(client);
        for (var attempt = 0; attempt < 12; attempt++)
            (await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone = "+998909999999" }, csrf)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        var denied = await AccessDatabaseFixture.PostAsync(client, "/api/auth/telegram/code", new { phone = "+998909999999" }, csrf);
        denied.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        denied.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    private WebApiTestHost<ApiAssemblyMarker> Factory(FakeTelegram bot)
    {
        var factory = new WebApiTestHost<ApiAssemblyMarker>
        {
            ConfigureHostHook = host => host.UseEnvironment(Environments.Development),
            ConfigureConfigurationHook = config => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = database.Connection,
                ["DataProtection:KeysPath"] = database.KeysPath,
                ["DataProtection:ApplicationName"] = "telegram-access-tests",
                ["AllowedHosts"] = "localhost",
                ["Identity:Telegram:Enabled"] = "true",
                ["Identity:Telegram:BotUsername"] = "ocharo_test_bot",
                ["Identity:Telegram:BotToken"] = "123456789:synthetic_token_for_offline_tests_ABC",
                ["Identity:Telegram:WebhookSecret"] = Secret,
                ["Identity:Telegram:PollingEnabled"] = "false"
            }),
            ConfigureServicesHook = services =>
            {
                services.RemoveAll<ITelegramBotClient>();
                services.AddSingleton<ITelegramBotClient>(new TelegramBotClient("123456789:synthetic_token_for_offline_tests_ABC", new HttpClient(bot)));
            }
        };
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        return factory;
    }

    private async Task<AccessUser> LinkedUserAsync(WebApiTestHost<ApiAssemblyMarker> factory, string phone, long chat)
    {
        var user = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        await database.ExecuteAsync("INSERT INTO access.telegram_links(user_id,telegram_id,phone,linked_at) VALUES ($1,$2,$3,now())", user.Id, chat, phone);
        return user;
    }

    private static object Message(long chat, string? text = null, long? contactUser = null, string? phone = null, string chatType = "private")
    {
        return new { message_id = 1, date = 1700000000, from = new { id = chat, is_bot = false, first_name = "Operator" },
            chat = new { id = chat, type = chatType }, text, contact = contactUser.HasValue ? new { phone_number = phone, first_name = "Operator", user_id = contactUser.Value } : null };
    }

    private static async Task<HttpResponseMessage> UpdateAsync(HttpClient client, object body, string secret = Secret)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/telegram/updates") { Content = JsonContent.Create(body) };
        request.Headers.Add("X-Telegram-Bot-Api-Secret-Token", secret);
        return await client.SendAsync(request);
    }

    private static async Task<string> CsrfAsync(HttpClient client)
    {
        return (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("csrfToken").GetString()!;
    }

    private static async Task<JsonElement> MetadataAsync(HttpClient client)
    {
        return await DataAsync(await client.GetAsync("/api/auth/telegram"));
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private sealed class FakeTelegram : HttpMessageHandler
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public bool RejectDelivery { get; init; }
        public string LastCode()
        {
            return Messages.Select(text => Regex.Match(text, "Ocharo sign-in code: ([0-9]{6})")).Last(match => match.Success).Groups[1].Value;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri!.AbsolutePath.Should().EndWith("/sendMessage");
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            Messages.Enqueue(document.RootElement.GetProperty("text").GetString()!);
            if (RejectDelivery) return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = JsonContent.Create(new { ok = false, error_code = 403, description = "bot blocked in synthetic test" }) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { ok = true,
                result = new { message_id = 1, date = 1700000000, chat = new { id = Chat, type = "private" }, text = "accepted" } }) };
        }
    }
}
