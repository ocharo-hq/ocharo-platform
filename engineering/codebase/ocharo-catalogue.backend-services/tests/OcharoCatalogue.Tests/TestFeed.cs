using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;

namespace OcharoCatalogue.Tests;

/// <summary>The public feed.</summary>
public sealed class TestFeed(CatalogueClient client)
{
    public Task<PublicCatalogue> BuildAsync(string? locale, CancellationToken ct) => client.SendAsync(new PublicCatalogueGetQuery { Locale = locale }, ct);
}
