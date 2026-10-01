using Microsoft.AspNetCore.Mvc;
using OcharoBrand.Application.Brand;

namespace OcharoBrand.Api.Brand.Controllers;

/// <summary>
/// The kit every Ocharo app reads. Send <c>X-Ocharo-App: catalogue</c> (or <c>?app=</c>) to appear on the Brand
/// "consumers" panel. Stable logo URLs follow publishes and appearances; media URLs are immutable.
/// </summary>
[ApiController]
[Route("api/public/brand")]
public sealed class PublicBrandController(PublicBrandService brand, IMediaStore media) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? locale, [FromQuery] string? app, CancellationToken ct)
    {
        var kit = await brand.BuildAsync(locale, ct);
        await brand.TouchAsync(app ?? Request.Headers["X-Ocharo-App"].ToString(), "/api/public/brand", kit.Release, ct);
        await CacheAsync(60, ct);
        return Ok(kit);
    }

    [HttpGet("releases/{version:int}")]
    public async Task<IActionResult> Release(int version, [FromQuery] string? locale, CancellationToken ct)
    {
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Ok(await brand.BuildAsync(locale, ct, version));
    }

    /// <summary>Stable logo address: always the effective file for the slot right now (5-minute cache, shorter near an appearance boundary).</summary>
    [HttpGet("logo/{slot}")]
    public async Task<IActionResult> Logo(string slot, [FromQuery] bool preview, [FromQuery] string? app, CancellationToken ct)
    {
        var logo = await brand.EffectiveLogoAsync(slot, ct);
        if (logo is null) return NotFound();
        var opened = await media.OpenAsync(logo.Sha256, preview, ct);
        if (opened is null) return NotFound();
        var release = (await brand.LatestAsync(ct))?.Version ?? 0;
        await brand.TouchAsync(app ?? Request.Headers["X-Ocharo-App"].ToString(), $"/api/public/brand/logo/{slot}", release, ct);
        await CacheAsync(300, ct);
        Response.Headers.ETag = $"\"{logo.Sha256}{(preview ? "-p" : "")}\"";
        var (record, content) = opened.Value;
        return File(content, preview && record.PreviewPath is not null ? "image/webp" : record.ContentType);
    }

    [HttpGet("media/{sha256}")]
    public Task<IActionResult> Media(string sha256, CancellationToken ct) => ServeAsync(sha256, false, ct);

    [HttpGet("media/{sha256}/preview")]
    public Task<IActionResult> Preview(string sha256, CancellationToken ct) => ServeAsync(sha256, true, ct);

    /// <summary>Palette and type stacks as CSS custom properties for any app to link.</summary>
    [HttpGet("tokens.css")]
    public async Task<IActionResult> Tokens(CancellationToken ct)
    {
        await CacheAsync(300, ct);
        return Content(await brand.TokensCssAsync(ct), "text/css; charset=utf-8");
    }

    /// <summary>Caps freshness at the next appearance start/end so no cache outlives a scheduled change.</summary>
    private async Task CacheAsync(int seconds, CancellationToken ct)
    {
        var maxAge = await brand.SecondsToNextBoundaryAsync(ct) is { } boundary ? Math.Min(seconds, boundary) : seconds;
        Response.Headers.CacheControl = maxAge > 0 ? $"public, max-age={maxAge}" : "no-cache";
    }

    private async Task<IActionResult> ServeAsync(string sha256, bool preview, CancellationToken ct)
    {
        if (!await brand.IsDisclosedAsync(sha256, ct)) return NotFound();
        var opened = await media.OpenAsync(sha256, preview, ct);
        if (opened is null) return NotFound();
        var (record, content) = opened.Value;
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(content, preview && record.PreviewPath is not null ? "image/webp" : record.ContentType);
    }
}
