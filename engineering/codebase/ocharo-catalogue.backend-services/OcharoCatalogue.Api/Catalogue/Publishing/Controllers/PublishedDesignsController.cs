using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;
using WoW.Two.Sdk.Beta.Be.Web.SecureHeaders;

namespace OcharoCatalogue.Api.Catalogue.Publishing.Controllers;

/// <summary>Exposes published catalogue editions and their frozen artifacts over HTTP.</summary>
/// <remarks>An artifact held in WoW2 Files answers a redirect to a short-lived Files link.</remarks>
[ApiController]
[AllowAnonymous]
[Route("api/public/catalogue/editions")]
public sealed class PublishedDesignsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the published editions of one language.</summary>
    /// <param name="locale">The editions' language; Uzbek when omitted.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? locale, CancellationToken ct)
    {
        var result = await sender.SendAsync(new EditionGetAllQuery { Locale = locale }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<PublishedEditionSummary>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets one published edition, the current one or a pinned release.</summary>
    /// <param name="slug">The catalogue slug.</param>
    /// <param name="locale">The edition's language; Uzbek when omitted.</param>
    /// <param name="release">The pinned release; the current one when omitted.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(
        string slug,
        [FromQuery] string? locale,
        [FromQuery] Guid? release,
        CancellationToken ct)
    {
        var query = new EditionGetBySlugQuery { Slug = slug, Locale = locale, ReleaseId = release };
        var result = await sender.SendAsync(query, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<PublishedEdition>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>
    /// Gets a published release's PDF or page image; Blog and other Ocharo origins embed and link them.
    /// </summary>
    /// <param name="slug">The catalogue slug.</param>
    /// <param name="asset">The artifact's name.</param>
    /// <param name="locale">The edition's language.</param>
    /// <param name="release">The release id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{slug}/assets/{asset}")]
    [AllowCrossOriginEmbedding]
    public async Task<IActionResult> GetAsset(
        string slug,
        string asset,
        [FromQuery] string locale,
        [FromQuery] Guid release,
        CancellationToken ct)
    {
        var query = new EditionAssetGetQuery
        {
            Slug = slug,
            Locale = locale,
            ReleaseId = release,
            Asset = asset,
        };
        var result = await sender.SendAsync(query, ct);

        // Never cached, a redirect included: each request asks whether the release is still published.
        Response.Headers.CacheControl = "no-store";
        return result.Match<IActionResult>(
            ok => Serve(ok.Data),
            fail => this.ToProblem(fail.Error));
    }

    private IActionResult Serve(CataloguePublicAsset asset)
    {
        if (asset.Location is { } location)
        {
            return Redirect(location.AbsoluteUri);
        }

        Response.Headers.ETag = $"\"{asset.Sha256}\"";
        return File(
            asset.Bytes!,
            asset.ContentType,
            asset.FileName);
    }
}
