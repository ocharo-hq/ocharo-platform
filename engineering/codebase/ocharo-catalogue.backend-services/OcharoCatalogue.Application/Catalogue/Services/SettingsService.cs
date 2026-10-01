using System.Text.RegularExpressions;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Brand catalogue settings; falls back to the Ocharo defaults until first saved.</summary>
public sealed partial class SettingsService(ICatalogueRepository repository)
{
    public const string SettingsId = "catalogue";

    public async Task<CatalogueSettings> GetAsync(CancellationToken ct)
    {
        var record = await repository.FindSettingsAsync(ct);
        return record is null ? CatalogueSettings.Default() : CatalogueJson.Read<CatalogueSettings>(record.Json);
    }

    public async Task<(CatalogueSettings Settings, int Revision)> GetWithRevisionAsync(CancellationToken ct)
    {
        var record = await repository.FindSettingsAsync(ct);
        return record is null ? (CatalogueSettings.Default(), 0) : (CatalogueJson.Read<CatalogueSettings>(record.Json), record.Revision);
    }

    public async Task<(CatalogueSettings Settings, int Revision)> SaveAsync(CatalogueSettings settings, int expectedRevision, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.BrandName) || settings.BrandName.Length > 80) throw new CatalogueException("Brand name is required (max 80).");
        if (settings.Currency != "UZS") throw new CatalogueException("Only UZS pricing is supported in this version.");
        if (settings.GarmentTypes.Count == 0) throw new CatalogueException("Keep at least one garment type.");
        if (settings.GarmentTypes.Select(g => g.Key).Distinct().Count() != settings.GarmentTypes.Count) throw new CatalogueException("Garment type keys must be unique.");
        foreach (var type in settings.GarmentTypes)
        {
            if (!Key().IsMatch(type.Key)) throw new CatalogueException($"Garment key '{type.Key}' must be a lowercase slug.");
            if (string.IsNullOrWhiteSpace(type.Uz)) throw new CatalogueException($"Garment '{type.Key}' needs an Uzbek name.");
        }

        var existing = await repository.GetProductsAsync(ct);
        var used = existing.Select(p => p.GarmentType).Distinct().Where(k => settings.GarmentTypes.All(g => g.Key != k)).ToList();
        if (used.Count > 0) throw new CatalogueException($"Garment types still used by products: {string.Join(", ", used)}.");

        var normalized = settings with
        {
            BrandName = settings.BrandName.Trim(),
            SizePreset = settings.SizePreset.Select(s => s.Trim()).Where(s => s.Length is > 0 and <= 12).Distinct().ToList(),
            ContactLines = settings.ContactLines.Select(s => s.Trim()).Where(s => s.Length is > 0 and <= 200).Take(8).ToList(),
            PriceNote = string.IsNullOrWhiteSpace(settings.PriceNote) ? null : settings.PriceNote.Trim(),
        };
        var saved = await repository.SaveSettingsAsync(new SettingsRecord { Id = SettingsId, Revision = expectedRevision + 1, Json = CatalogueJson.Write(normalized) },
            expectedRevision == 0 ? null : expectedRevision, ct);
        return (normalized, saved.Revision);
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Key();
}
