using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Services;

namespace OcharoCatalogue.Api.Catalogue.Controllers;

/// <summary>Ordered product selections.</summary>
[ApiController]
[Route("api/catalogue/collections")]
public sealed class CollectionsController(CollectionService collections) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await collections.ListAsync(ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct) => Ok(await collections.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CollectionInput input, CancellationToken ct) => Ok(await collections.SaveAsync(null, input, ct));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, CollectionInput input, CancellationToken ct) => Ok(await collections.SaveAsync(id, input, ct));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await collections.DeleteAsync(id, ct);
        return NoContent();
    }
}
