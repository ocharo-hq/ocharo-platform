using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Services;

namespace OcharoCatalogue.Api.Catalogue.Controllers;

/// <summary>Product grid, editor, history, images and bulk edits.</summary>
[ApiController]
[Route("api/catalogue/products")]
public sealed class ProductsController(ProductService products) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] string? garmentType,
        [FromQuery] string? status,
        [FromQuery] string? collection,
        [FromQuery] string? tag,
        CancellationToken ct)
    {
        return Ok(await products.ListAsync(new ProductFilter(
            q,
            garmentType,
            status,
            collection,
            tag), ct));
    }

    [HttpGet("stock")]
    public async Task<IActionResult> Stock(CancellationToken ct)
    {
        return Ok(await products.StockRowsAsync(ct));
    }

    [HttpPut("{id}/stock")]
    public async Task<IActionResult> UpdateStock(
        string id,
        StockInput input,
        CancellationToken ct)
    {
        return Ok(await products.UpdateStockAsync(
            id,
            input,
            ct));
    }

    [HttpGet("copy")]
    public async Task<IActionResult> Copy(CancellationToken ct)
    {
        return Ok(await products.CopyRowsAsync(ct));
    }

    [HttpPut("{id}/copy")]
    public async Task<IActionResult> UpdateCopy(
        string id,
        CopyInput input,
        CancellationToken ct)
    {
        return Ok(await products.UpdateCopyAsync(
            id,
            input,
            ct));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        return Ok(await products.GetAsync(id, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProductInput input, CancellationToken ct)
    {
        return Ok(await products.CreateAsync(input, ct));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        ProductInput input,
        CancellationToken ct)
    {
        return Ok(await products.UpdateAsync(
            id,
            input,
            ct));
    }

    [HttpPost("{id}/duplicate")]
    public async Task<IActionResult> Duplicate(string id, CancellationToken ct)
    {
        return Ok(await products.DuplicateAsync(id, ct));
    }

    [HttpPost("{id}/media")]
    [RequestSizeLimit(40 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        string id,
        [FromForm] IFormFile file,
        [FromForm] string role,
        [FromForm] int? expectedRevision,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return Ok(await products.AddMediaAsync(
            id,
            buffer.ToArray(),
            file.FileName,
            role,
            expectedRevision,
            ct));
    }

    [HttpGet("{id}/revisions")]
    public async Task<IActionResult> Revisions(string id, CancellationToken ct)
    {
        return Ok(await products.RevisionsAsync(id, ct));
    }

    [HttpGet("{id}/revisions/{revision:int}")]
    public async Task<IActionResult> Revision(
        string id,
        int revision,
        CancellationToken ct)
    {
        return Ok(await products.GetRevisionAsync(
            id,
            revision,
            ct));
    }

    [HttpPost("{id}/revisions/{revision:int}/restore")]
    public async Task<IActionResult> Restore(
        string id,
        int revision,
        RestoreInput input,
        CancellationToken ct)
    {
        return Ok(await products.RestoreAsync(
            id,
            revision,
            input.ExpectedRevision,
            ct));
    }

    [HttpPost("bulk/status")]
    public async Task<IActionResult> BulkStatus(BulkStatusInput input, CancellationToken ct)
    {
        return Ok(await products.SetStatusAsync(input, ct));
    }

    [HttpPost("bulk/pricing")]
    public async Task<IActionResult> BulkPricing(BulkPricingInput input, CancellationToken ct)
    {
        return Ok(await products.AdjustPricesAsync(input, ct));
    }

    public sealed record RestoreInput(int ExpectedRevision);
}
