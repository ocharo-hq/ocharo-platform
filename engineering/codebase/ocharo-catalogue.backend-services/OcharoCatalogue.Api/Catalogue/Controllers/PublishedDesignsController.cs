using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Services;
namespace OcharoCatalogue.Api.Catalogue.Controllers;

/// <summary>Read-only, explicitly published local catalogue releases and frozen PDF artifacts.</summary>
[ApiController]
[Route("api/public/catalogue/editions")]
public sealed class PublishedDesignsController(CatalogueReleaseService releases):ControllerBase
{
    [HttpGet] public async Task<IActionResult> List([FromQuery]string? locale,CancellationToken ct)=>Ok(await releases.PublicListAsync(locale,ct));
    [HttpGet("{slug}")] public async Task<IActionResult> Read(string slug,[FromQuery]string? locale,[FromQuery]Guid? release,CancellationToken ct)=>Ok(await releases.PublicAsync(slug,locale,release,ct));
    [HttpGet("{slug}/assets/{asset}")]
    public async Task<IActionResult> Asset(string slug,string asset,[FromQuery]string locale,[FromQuery]Guid release,CancellationToken ct)
    {
        var result=await releases.PublicAssetAsync(slug,locale,release,asset,ct);Response.Headers.CacheControl="no-store";Response.Headers.ETag=$"\"{result.Sha256}\"";
        return File(result.Bytes,result.ContentType,asset);
    }
}
