using OcharoCatalogue.Application.Catalogue.Settings.Commands;
using OcharoCatalogue.Application.Catalogue.Settings.Models;
using OcharoCatalogue.Application.Catalogue.Settings.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Tests;

/// <summary>Settings commands and queries.</summary>
public sealed class TestSettings(CatalogueClient client)
{
    public async Task<CatalogueSettings> GetAsync(CancellationToken ct) => (await client.SendAsync(new SettingsGetQuery(), ct)).Settings;
    public Task<CatalogueSettingsDetail> SaveAsync(CatalogueSettings settings, int expectedRevision, CancellationToken ct) =>
        client.SendAsync(new SettingsUpdateCommand { Settings = settings, ExpectedRevision = expectedRevision }, ct);
}
