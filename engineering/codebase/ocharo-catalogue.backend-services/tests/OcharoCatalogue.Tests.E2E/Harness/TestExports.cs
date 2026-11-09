using OcharoCatalogue.Application.Catalogue.Exporting.Queries;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>Exports.</summary>
public sealed class TestExports(CatalogueClient client)
{
    public Task<byte[]> JsonBundleAsync(CancellationToken ct) => client.SendAsync(new ExportJsonGetQuery(), ct);
    public Task<byte[]> CsvAsync(CancellationToken ct) => client.SendAsync(new ExportCsvGetQuery(), ct);
    public Task<byte[]> WorkspaceSchemaZipAsync(CancellationToken ct) => client.SendAsync(new ExportWorkspaceGetQuery(), ct);
}
