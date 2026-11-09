using OcharoCatalogue.Application.Catalogue.Importing.Commands;
using OcharoCatalogue.Application.Catalogue.Importing.Models;

namespace OcharoCatalogue.Tests;

/// <summary>The workspace import.</summary>
public sealed class TestImporter(CatalogueClient client)
{
    public Task<ImportReport> ImportAsync(ImportInput input, CancellationToken ct) => client.SendAsync(new WorkspaceImportCommand { Input = input }, ct);
}
