using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Services;
using WoW.Two.Sdk.Beta.Be.Web.SecureHeaders;

namespace OcharoCatalogue.Api.Catalogue.Controllers;

/// <summary>Read-only, explicitly published local catalogue releases and frozen PDF artifacts; an artifact held in WoW2
/// Files answers a redirect to a short-lived Files link.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/catalogue/editions")]
public sealed class PublishedDesignsController(CatalogueReleaseService releases) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? locale, CancellationToken ct)
    {
        return Ok(await releases.PublicListAsync(locale, ct));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Read(
        string slug,
        [FromQuery] string? locale,
        [FromQuery] Guid? release,
        CancellationToken ct)
    {
        return Ok(await releases.PublicAsync(
            slug,
            locale,
            release,
            ct));
    }

    /// <summary>Serves a published release's PDF or page image; Blog and other Ocharo origins embed and link them.</summary>
    [HttpGet("{slug}/assets/{asset}")]
    [AllowCrossOriginEmbedding]
    public async Task<IActionResult> Asset(
        string slug,
        string asset,
        [FromQuery] string locale,
        [FromQuery] Guid release,
        CancellationToken ct)
    {
        var result = await releases.PublicAssetAsync(
            slug,
            locale,
            release,
            asset,
            ct);
        // Never cached, a redirect included: each request asks whether the release is still published.
        Response.Headers.CacheControl = "no-store";
        if (result.Location is { } location)
            return Redirect(location.AbsoluteUri);
        Response.Headers.ETag = $"\"{result.Sha256}\"";
        return File(
            result.Bytes!,
            result.ContentType,
            result.FileName);
    }
}
