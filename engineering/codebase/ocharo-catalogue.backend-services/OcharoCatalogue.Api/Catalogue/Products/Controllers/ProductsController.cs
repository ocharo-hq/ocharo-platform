using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Api.Catalogue.Core.Extensions;
using OcharoCatalogue.Api.Catalogue.Products.Requests;
using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Application.Catalogue.Products.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoCatalogue.Api.Catalogue.Products.Controllers;

/// <summary>Exposes catalogue products over HTTP.</summary>
[ApiController]
[Route("api/catalogue/products")]
public sealed class ProductsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the product grid's rows.</summary>
    /// <param name="q">The text to search for.</param>
    /// <param name="garmentType">The garment family to keep.</param>
    /// <param name="status">The status to keep.</param>
    /// <param name="collection">The collection whose products to keep.</param>
    /// <param name="tag">The tag to keep.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? q,
        [FromQuery] string? garmentType,
        [FromQuery] string? status,
        [FromQuery] string? collection,
        [FromQuery] string? tag,
        CancellationToken ct)
    {
        var filter = new ProductFilter(
            q,
            garmentType,
            status,
            collection,
            tag);
        var result = await sender.SendAsync(new ProductGetAllQuery { Filter = filter }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<ProductSummary>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the stock sheet.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("stock")]
    public async Task<IActionResult> GetStock(CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductStockGetAllQuery(), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<StockRow>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Saves a product's stock tracking.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="input">The stock change.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPut("{id}/stock")]
    public async Task<IActionResult> UpdateStock(
        string id,
        StockInput input,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductStockUpdateCommand { Id = id, Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<StockRow>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets every product's copy for the translation grid.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("copy")]
    public async Task<IActionResult> GetCopy(CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductCopyGetAllQuery(), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<ProductCopyRow>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Saves a product's localized copy.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="input">The copy change.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPut("{id}/copy")]
    public async Task<IActionResult> UpdateCopy(
        string id,
        CopyInput input,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductCopyUpdateCommand { Id = id, Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ProductCopyRow>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a product by id.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductGetByIdQuery { Id = id }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ProductDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Creates a product.</summary>
    /// <param name="input">The product fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost]
    public async Task<IActionResult> Create(ProductInput input, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductCreateCommand { Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ProductDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Updates a product.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="input">The product fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateById(
        string id,
        ProductInput input,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductUpdateCommand { Id = id, Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ProductDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Copies a product into the next draft variant.</summary>
    /// <param name="id">The id of the product to copy.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("{id}/duplicate")]
    public async Task<IActionResult> Duplicate(string id, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductDuplicateCommand { Id = id }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ProductDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Uploads an image and appends it to a product.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="file">The uploaded image.</param>
    /// <param name="role">What the image shows.</param>
    /// <param name="expectedRevision">The revision the caller saw.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("{id}/media")]
    [RequestSizeLimit(40 * 1024 * 1024)]
    public async Task<IActionResult> CreateMedia(
        string id,
        [FromForm] IFormFile file,
        [FromForm] string role,
        [FromForm] int? expectedRevision,
        CancellationToken ct)
    {
        var command = new ProductMediaAddCommand
        {
            Id = id,
            Bytes = await file.ReadAllBytesAsync(ct),
            FileName = file.FileName,
            Role = role,
            ExpectedRevision = expectedRevision,
        };
        var result = await sender.SendAsync(command, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ProductDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a product's saved revisions.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{id}/revisions")]
    public async Task<IActionResult> GetRevisions(string id, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductRevisionGetAllQuery { Id = id }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<RevisionSummary>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the document a product had at one saved revision.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="revision">The revision number.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{id}/revisions/{revision:int}")]
    public async Task<IActionResult> GetRevisionById(
        string id,
        int revision,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductRevisionGetByIdQuery { Id = id, Revision = revision }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ProductDocument>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Saves an older revision as the product's newest one.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="revision">The revision to restore.</param>
    /// <param name="request">The revision the caller edited.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("{id}/revisions/{revision:int}/restore")]
    public async Task<IActionResult> Restore(
        string id,
        int revision,
        RestoreProductApiRequest request,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(request.ToCommand(id, revision), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ProductDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Sets one status on several products.</summary>
    /// <param name="input">The products and their new status.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("bulk/status")]
    public async Task<IActionResult> SetStatus(BulkStatusInput input, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductStatusBulkSetCommand { Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<BulkResult>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Adjusts the prices of several products.</summary>
    /// <param name="input">The products and the price change.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("bulk/pricing")]
    public async Task<IActionResult> AdjustPricing(BulkPricingInput input, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ProductPricingBulkAdjustCommand { Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<BulkResult>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }
}
