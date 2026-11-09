using BrandWorkspace.Api.Foundation.Filters;
using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Application.Files.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;
using WoW.Two.Sdk.Beta.Be.Web.SecureHeaders;

namespace BrandWorkspace.Api.Commerce.Controllers;

/// <summary>Exposes released brands, their catalogues and approved media over HTTP.</summary>
/// <remarks>
/// Other origins embed these: Blog's catalogue pages show edition page images and link the PDFs, and published product
/// media load in Marketing and Blog, so the routes and their Files redirects answer <c>Cross-Origin-Resource-Policy:
/// cross-origin</c>. The release check decides each time, and every answer stays no-store.
/// </remarks>
[ApiController]
[Route("api/public/v1/brands/{slug}")]
[AllowAnonymous]
[AllowCrossOriginEmbedding]
[NoStore]
public sealed class PublicBrandsController(ISender sender) : ControllerBase
{
    /// <summary>Gets a released brand's public identity.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(string slug, [FromQuery] Guid? release, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new PublicBrandGetQuery { Slug = slug, Release = release },
            cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ok.Data), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a released brand's public catalogue in one language.</summary>
    [HttpGet("catalogue")]
    public async Task<IActionResult> GetCatalogue(string slug, [FromQuery] string? locale, [FromQuery] Guid? release,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new PublicCatalogueGetQuery { Slug = slug, Locale = locale, Release = release },
            cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ok.Data), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Opens a released edition's PDF or page artifact; with WoW2 Files it redirects to a link.</summary>
    [HttpGet("catalogue-editions/{editionId:guid}/{asset}")]
    public async Task<IActionResult> OpenEditionAsset(
        string slug,
        Guid editionId,
        string asset,
        [FromQuery] Guid? release,
        [FromQuery] string? download,
        CancellationToken cancellationToken)
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        var result = await sender.SendAsync(new PublicEditionAssetOpenQuery
        {
            Slug = slug,
            EditionId = editionId,
            Asset = asset,
            Release = release,
            Download = download == "1",
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Stored(ok.Data.Content, ok.Data.FileName),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Opens a released brand's approved media; with WoW2 Files it redirects to a link.</summary>
    [HttpGet("media/{sha256}")]
    public async Task<IActionResult> OpenMedia(
        string slug,
        string sha256,
        [FromQuery] Guid? release,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new PublicMediaOpenQuery { Slug = slug, Sha256 = sha256, Release = release },
            cancellationToken);
        return result.Match<IActionResult>(
            ok =>
            {
                Response.Headers.XContentTypeOptions = "nosniff";
                return Stored(ok.Data, null);
            },
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>
    /// Answers stored content: a redirect to its Files link, else its bytes, saved under <paramref name="fileName"/>
    /// when one is given.
    /// </summary>
    private IActionResult Stored(StoredContent content, string? fileName)
    {
        return content.Location is { } location
            ? Redirect(location.AbsoluteUri)
            : File(content.Bytes!, content.ContentType, fileName);
    }
}
