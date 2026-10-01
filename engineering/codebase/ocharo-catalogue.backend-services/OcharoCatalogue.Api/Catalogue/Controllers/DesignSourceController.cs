using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;
namespace OcharoCatalogue.Api.Catalogue.Controllers;

[ApiController]
[Route("api/catalogue/design-source")]
public sealed class DesignSourceController(ICatalogueRepository repository,SettingsService settings):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct)
    {
        var rows=await repository.GetProductsAsync(ct);var collections=(await repository.GetCollectionsAsync(ct)).Select(c=>CatalogueJson.Read<CollectionDocument>(c.Json)).ToArray();
        var products=rows.OrderBy(r=>r.SortOrder).ThenBy(r=>r.Id,StringComparer.Ordinal).Select(r=>{
            var d=CatalogueJson.Read<ProductDocument>(r.Json);return new ProductDetail(d,r.Revision,r.CreatedAt,r.UpdatedAt,d.Media.Select(m=>new MediaView(m,CatalogueUrls.Media(m.Sha256),m.HasPreview?CatalogueUrls.Preview(m.Sha256):null)).ToArray(),collections.Where(c=>c.ProductIds.Contains(d.Id)).Select(c=>c.Id).ToArray());
        }).ToArray();
        return Ok(new {products,settings=await settings.GetAsync(ct)});
    }
}
