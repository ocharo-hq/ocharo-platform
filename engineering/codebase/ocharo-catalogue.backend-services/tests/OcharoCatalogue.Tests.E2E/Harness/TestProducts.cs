using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Application.Catalogue.Products.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>Product commands and queries, shaped like the calls the tests make.</summary>
public sealed class TestProducts(CatalogueClient client)
{
    public Task<IReadOnlyList<ProductSummary>> ListAsync(ProductFilter filter, CancellationToken ct) => client.SendAsync(new ProductGetAllQuery { Filter = filter }, ct);
    public Task<ProductDetail> GetAsync(string id, CancellationToken ct) => client.SendAsync(new ProductGetByIdQuery { Id = id }, ct);
    public Task<ProductDetail> CreateAsync(ProductInput input, CancellationToken ct) => client.SendAsync(new ProductCreateCommand { Input = input }, ct);
    public Task<ProductDetail> UpdateAsync(string id, ProductInput input, CancellationToken ct) => client.SendAsync(new ProductUpdateCommand { Id = id, Input = input }, ct);
    public Task<ProductDetail> DuplicateAsync(string id, CancellationToken ct) => client.SendAsync(new ProductDuplicateCommand { Id = id }, ct);
    public Task<ProductDetail> AddMediaAsync(string id, byte[] bytes, string fileName, string role, int? expectedRevision, CancellationToken ct) =>
        client.SendAsync(new ProductMediaAddCommand { Id = id, Bytes = bytes, FileName = fileName, Role = role, ExpectedRevision = expectedRevision }, ct);
    public Task<IReadOnlyList<RevisionSummary>> RevisionsAsync(string id, CancellationToken ct) => client.SendAsync(new ProductRevisionGetAllQuery { Id = id }, ct);
    public Task<ProductDocument> GetRevisionAsync(string id, int revision, CancellationToken ct) => client.SendAsync(new ProductRevisionGetByIdQuery { Id = id, Revision = revision }, ct);
    public Task<ProductDetail> RestoreAsync(string id, int revision, int expectedRevision, CancellationToken ct) =>
        client.SendAsync(new ProductRestoreCommand { Id = id, Revision = revision, ExpectedRevision = expectedRevision }, ct);
    public Task<BulkResult> SetStatusAsync(BulkStatusInput input, CancellationToken ct) => client.SendAsync(new ProductStatusBulkSetCommand { Input = input }, ct);
    public Task<BulkResult> AdjustPricesAsync(BulkPricingInput input, CancellationToken ct) => client.SendAsync(new ProductPricingBulkAdjustCommand { Input = input }, ct);
    public Task<IReadOnlyList<ProductCopyRow>> CopyRowsAsync(CancellationToken ct) => client.SendAsync(new ProductCopyGetAllQuery(), ct);
    public Task<ProductCopyRow> UpdateCopyAsync(string id, CopyInput input, CancellationToken ct) => client.SendAsync(new ProductCopyUpdateCommand { Id = id, Input = input }, ct);
    public Task<IReadOnlyList<StockRow>> StockRowsAsync(CancellationToken ct) => client.SendAsync(new ProductStockGetAllQuery(), ct);
    public Task<StockRow> UpdateStockAsync(string id, StockInput input, CancellationToken ct) => client.SendAsync(new ProductStockUpdateCommand { Id = id, Input = input }, ct);
}
