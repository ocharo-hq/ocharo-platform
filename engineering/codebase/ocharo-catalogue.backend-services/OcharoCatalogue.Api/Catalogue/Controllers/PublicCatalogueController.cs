using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Services;

namespace OcharoCatalogue.Api.Catalogue.Controllers;

/// <summary>Read-only feed for sibling platforms: active products and published collections.</summary>
[ApiController]
[Route("api/public/catalogue")]
public sealed class PublicCatalogueController(PublicCatalogueService feed) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? locale, CancellationToken ct)
    {
        return Ok(await feed.BuildAsync(locale, ct));
    }
}
