using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;
using WoW.Two.Sdk.Beta.Be.Web.SecureHeaders;

namespace OcharoBrand.Api.Brand.Controllers;

/// <summary>Exposes the published brand kits over HTTP.</summary>
/// <remarks>
/// Send <c>X-Ocharo-App: catalogue</c> (or <c>?app=</c>) to appear on the Brand "consumers" panel. Stable logo URLs follow
/// publishes and appearances; media URLs are immutable. Other origins embed its logos, media and tokens without CORS (an
/// image, a stylesheet, a social card), so every route allows it. The kit travels bare, outside the API's
/// <c>{ data }</c> envelope, since other Ocharo apps read it as it is.
/// </remarks>
[ApiController]
[AllowAnonymous]
[AllowCrossOriginEmbedding]
[Route("api/public/brand")]
public sealed class PublicBrandsController(ISender sender) : ControllerBase
{
    /// <summary>The header a reading app names itself in.</summary>
    private const string AppHeader = "X-Ocharo-App";

    /// <summary>Gets the latest kit with today's appearance (60-second cache).</summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? locale,
        [FromQuery] string? app,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new PublicBrandGetQuery { Locale = locale }, ct);
        return await result.Match(
            success => ReadAsync(app, "/api/public/brand", success.Data.Release, 60, () => Ok(success.Data), ct),
            failure => Task.FromResult<IActionResult>(this.ToProblem(failure.Error)));
    }

    /// <summary>Gets a pinned release, exactly as published (cached a year).</summary>
    [HttpGet("releases/{version:int}")]
    public async Task<IActionResult> Release(
        int version,
        [FromQuery] string? locale,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new PublicBrandGetQuery { Locale = locale, Version = version }, ct);
        return result.Match<IActionResult>(
            success =>
            {
                Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return Ok(success.Data);
            },
            failure => this.ToProblem(failure.Error));
    }

    /// <summary>Stable logo address: always the effective file for the slot right now (5-minute cache, shorter near an appearance boundary).</summary>
    [HttpGet("logo/{slot}")]
    public async Task<IActionResult> Logo(
        string slot,
        [FromQuery] bool preview,
        [FromQuery] string? app,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new PublicLogoOpenQuery { Slot = slot, Preview = preview }, ct);
        // At most 5 minutes either way, so a cached redirect never outlives its Files link.
        return await result.Match(
            success => ReadAsync(app, $"/api/public/brand/logo/{slot}", success.Data.Release, MediaContent.RedirectMaxAgeSeconds,
                () => LogoFile(success.Data, preview), ct),
            failure => Task.FromResult<IActionResult>(this.ToProblem(failure.Error)));
    }

    /// <summary>A disclosed file, by its SHA-256.</summary>
    [HttpGet("media/{sha256}")]
    public async Task<IActionResult> Media(string sha256, CancellationToken ct)
    {
        return Serve(await sender.SendAsync(new PublicMediaOpenQuery { Sha256 = sha256 }, ct));
    }

    /// <summary>The WebP preview of a disclosed file.</summary>
    [HttpGet("media/{sha256}/preview")]
    public async Task<IActionResult> Preview(string sha256, CancellationToken ct)
    {
        return Serve(await sender.SendAsync(new PublicMediaOpenQuery { Sha256 = sha256, Preview = true }, ct));
    }

    /// <summary>Palette and type stacks as CSS custom properties for any app to link.</summary>
    [HttpGet("tokens.css")]
    public async Task<IActionResult> Tokens(CancellationToken ct)
    {
        var result = await sender.SendAsync(new TokensCssGetQuery(), ct);
        return await result.Match(
            success => CachedAsync(300, () => Content(success.Data, "text/css; charset=utf-8"), ct),
            failure => Task.FromResult<IActionResult>(this.ToProblem(failure.Error)));
    }

    /// <summary>Records the reading app against <paramref name="release"/>, then answers with a capped cache lifetime.</summary>
    private async Task<IActionResult> ReadAsync(string? app, string path, int release, int seconds, Func<IActionResult> respond, CancellationToken ct)
    {
        var command = new ConsumerTouchCommand { App = app ?? Request.Headers[AppHeader].ToString(), Path = path, Release = release };
        var touched = await sender.SendAsync(command, ct);
        if (touched is AppResult<Unit>.Failure failure)
            return this.ToProblem(failure.Error);
        return await CachedAsync(seconds, respond, ct);
    }

    /// <summary>Caps freshness at the next appearance start/end so no cache outlives a scheduled change.</summary>
    private async Task<IActionResult> CachedAsync(int seconds, Func<IActionResult> respond, CancellationToken ct)
    {
        var result = await sender.SendAsync(new KitFreshnessGetQuery(), ct);
        return result.Match(
            success =>
            {
                var maxAge = success.Data.SecondsToNextBoundary is { } boundary ? Math.Min(seconds, boundary) : seconds;
                Response.Headers.CacheControl = maxAge > 0 ? $"public, max-age={maxAge}" : "no-cache";
                return respond();
            },
            failure => this.ToProblem(failure.Error));
    }

    /// <summary>Redirects to the logo's Files link, or serves its local bytes under an ETag of the file and variant.</summary>
    private IActionResult LogoFile(PublicLogoContent logo, bool preview)
    {
        if (logo.Media.Location is { } location)
            return Redirect(location.AbsoluteUri);
        Response.Headers.ETag = $"\"{logo.Logo.Sha256}{(preview ? "-p" : "")}\"";
        return File(logo.Media.Content!, logo.Media.ContentType);
    }

    /// <summary>Serves a disclosed file: a Files link expires, so only local bytes keep the immutable year; a redirect lives 5 minutes.</summary>
    private IActionResult Serve(AppResult<MediaContent> result)
    {
        return result.Match<IActionResult>(
            success =>
            {
                if (success.Data.Location is { } location)
                {
                    Response.Headers.CacheControl = $"public, max-age={MediaContent.RedirectMaxAgeSeconds}";
                    return Redirect(location.AbsoluteUri);
                }
                Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                return File(success.Data.Content!, success.Data.ContentType);
            },
            failure => this.ToProblem(failure.Error));
    }
}
