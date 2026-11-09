using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;

/// <summary>Provides the brand catalogue settings, which fall back to the Ocharo defaults until first saved.</summary>
public sealed class CatalogueSettingsService(ICatalogueRepository repository)
{
    /// <summary>Gets the current settings.</summary>
    /// <param name="ct">The token that cancels the read.</param>
    public async Task<CatalogueSettings> GetAsync(CancellationToken ct)
    {
        var record = await repository.FindSettingsAsync(ct);
        return record is null ? CatalogueSettings.Default() : CatalogueJson.Read<CatalogueSettings>(record.Json);
    }

    /// <summary>Gets the current settings with their revision; 0 until first saved.</summary>
    /// <param name="ct">The token that cancels the read.</param>
    public async Task<(CatalogueSettings Settings, int Revision)> GetWithRevisionAsync(CancellationToken ct)
    {
        var record = await repository.FindSettingsAsync(ct);
        if (record is null)
        {
            return (CatalogueSettings.Default(), 0);
        }

        return (CatalogueJson.Read<CatalogueSettings>(record.Json), record.Revision);
    }
}
