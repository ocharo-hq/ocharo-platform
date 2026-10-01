using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Exports: a full JSON backup, a CSV price sheet and per-product files in the workspace product schema.</summary>
public sealed class CatalogueExportService(ICatalogueRepository repository, SettingsService settings, IClock clock)
{
    public async Task<byte[]> JsonBundleAsync(CancellationToken ct)
    {
        var products = (await repository.GetProductsAsync(ct))
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => new { revision = r.Revision, updatedAt = r.UpdatedAt, document = CatalogueJson.Read<ProductDocument>(r.Json) });
        var collections = (await repository.GetCollectionsAsync(ct)).Select(c => CatalogueJson.Read<CollectionDocument>(c.Json));
        var bundle = new
        {
            format = "ocharo-catalogue/1",
            exportedAt = clock.UtcNow,
            settings = await settings.GetAsync(ct),
            products,
            collections,
        };
        return JsonSerializer.SerializeToUtf8Bytes(bundle, CatalogueJson.Indented);
    }

    public async Task<byte[]> CsvAsync(CancellationToken ct)
    {
        var builder = new StringBuilder();
        builder.AppendLine("id,garment_type,variant,status,name_uz,name_ru,name_en,sizes,retail_uzs,wholesale_uzs,wholesale_from_qty,pricing_status,open_issues");
        foreach (var record in (await repository.GetProductsAsync(ct)).OrderBy(r => r.SortOrder).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            var d = CatalogueJson.Read<ProductDocument>(record.Json);
            var bulk = d.Pricing?.Tiers.OrderByDescending(t => t.MinimumQuantity).FirstOrDefault(t => t.MinimumQuantity > 1);
            builder.AppendJoin(',', new[]
            {
                d.Id, d.GarmentType, d.VariantCode, d.Status, d.Locales.Uz.Name, d.Locales.Ru.Name, d.Locales.En.Name,
                string.Join(' ', d.Sizes), d.Pricing?.RetailPrice?.ToString(CultureInfo.InvariantCulture),
                bulk?.UnitPrice.ToString(CultureInfo.InvariantCulture), bulk?.MinimumQuantity.ToString(CultureInfo.InvariantCulture),
                d.Pricing?.Status, d.Issues.Count(i => i.Status == IssueStatus.Open).ToString(CultureInfo.InvariantCulture),
            }.Select(Csv));
            builder.AppendLine();
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    /// <summary>A ZIP of <c>{id}/och-{id}.json</c> files shaped like the workspace product schema (schema 1.3).</summary>
    public async Task<byte[]> WorkspaceSchemaZipAsync(CancellationToken ct)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var record in (await repository.GetProductsAsync(ct)).OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                var d = CatalogueJson.Read<ProductDocument>(record.Json);
                var entry = zip.CreateEntry($"{d.Id}/och-{d.Id}.json", CompressionLevel.Optimal);
                entry.LastWriteTime = record.UpdatedAt;
                await using var stream = entry.Open();
                await JsonSerializer.SerializeAsync(stream, ToWorkspaceSchema(d, record.UpdatedAt), CatalogueJson.Indented, ct);
            }
        }

        return output.ToArray();
    }

    /// <summary>Maps a product back to the workspace JSON schema; unknown source fields stay null rather than invented.</summary>
    public static JsonObject ToWorkspaceSchema(ProductDocument d, DateTimeOffset updatedAt)
    {
        JsonObject Locale(ProductLocale l) => new()
        {
            ["status"] = l.Status == LocaleStatus.Approved ? "approved" : "draft",
            ["name"] = l.Name, ["fabric"] = l.Fabric, ["decoration"] = l.Decoration, ["description"] = l.Description,
        };
        JsonArray Paths(string role) => new(d.Media.Where(m => m.Role == role).Select(m => (JsonNode?)(m.SourcePath ?? m.FileName)).ToArray());
        var pricing = d.Pricing is null ? null : new JsonObject
        {
            ["currency"] = d.Pricing.Currency,
            ["basis"] = "per_piece",
            ["status"] = d.Pricing.Status == PricingStatus.Confirmed ? "user_confirmed" : "draft",
            ["tiers"] = new JsonArray(d.Pricing.Tiers.Select(t => (JsonNode?)new JsonObject
            {
                ["minimum_quantity"] = t.MinimumQuantity, ["maximum_quantity"] = t.MaximumQuantity, ["unit_price"] = t.UnitPrice,
            }).ToArray()),
            ["wording_proposal"] = d.Pricing.Wording,
        };
        return new JsonObject
        {
            ["schema_version"] = "1.3",
            ["updated_at"] = updatedAt.ToString("O", CultureInfo.InvariantCulture),
            ["id"] = d.Id,
            ["brand"] = "OCHARO",
            ["source"] = new JsonObject
            {
                ["language"] = d.SourceVerbatim is null ? null : "uz",
                ["asset"] = d.Source?.Path,
                ["kind"] = d.SourceVerbatim is null ? "media_only" : "text",
                ["verbatim"] = d.SourceVerbatim,
            },
            ["locales"] = new JsonObject { ["uz"] = Locale(d.Locales.Uz), ["ru"] = Locale(d.Locales.Ru), ["en"] = Locale(d.Locales.En) },
            ["sizes"] = d.Sizes.Count == 0 ? null : new JsonArray(d.Sizes.Select(s => (JsonNode?)s).ToArray()),
            ["classification"] = new JsonObject
            {
                ["status"] = "confirmed",
                ["design_id"] = d.DesignId,
                ["variant_id"] = d.VariantCode,
                ["garment_type"] = d.GarmentType == "look" ? null : d.GarmentType,
                ["fabric_codes"] = new JsonArray(d.FabricCodes.Select(s => (JsonNode?)s).ToArray()),
                ["decoration_codes"] = new JsonArray(d.DecorationCodes.Select(s => (JsonNode?)s).ToArray()),
            },
            ["assets"] = new JsonObject
            {
                ["source_images"] = Paths(MediaRole.Photo),
                ["model_images"] = Paths(MediaRole.Model),
                ["cutout_images"] = Paths(MediaRole.Cutout),
                ["product_reference_images"] = Paths(MediaRole.Reference),
                ["baseline_id"] = null,
            },
            ["review"] = new JsonObject
            {
                ["status"] = d.Issues.Any(i => i.Status == IssueStatus.Open) ? "TBD" : "resolved",
                ["issues"] = new JsonArray(d.Issues.Select(i => (JsonNode?)new JsonObject
                {
                    ["field"] = i.Field, ["status"] = i.Status == IssueStatus.Open ? "TBD" : "resolved", ["question"] = i.Question,
                }).ToArray()),
            },
            ["pricing"] = pricing,
        };
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        // Neutralize spreadsheet formula injection and quote separators.
        var safe = value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
        return safe.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{safe.Replace("\"", "\"\"")}\"" : safe;
    }
}
