using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Importing.Models;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Options;
using OcharoCatalogue.Infrastructure.Catalogue.Importing.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Importing.FoundationServices;

/// <summary>
/// Provides the import of the workspace product records (<c>{id}/och-{id}.json</c>, schema 1.x) with their images.
/// Read-only against the source: hashes are recorded as provenance and nothing is written back.
/// </summary>
public sealed partial class WorkspaceImportService(
    ICatalogueRepository repository,
    IMediaStore media,
    CatalogueSettingsService settings,
    IOptions<CatalogueOptions> options,
    TimeProvider timeProvider)
{
    private static readonly string[] FamilyOrder = ["nimcha", "baska", "korset", "kostyum", "abaya", "look"];

    /// <summary>Imports the products of the requested or configured source folder.</summary>
    /// <param name="input">The import request.</param>
    /// <param name="ct">The token that cancels the import.</param>
    public async Task<AppResult<ImportReport>> ImportAsync(ImportInput input, CancellationToken ct)
    {
        var root = input.SourceRoot ?? options.Value.SourceRoot;
        if (root is null)
        {
            var error = CatalogueErrors.Invalid("No source folder is configured (Catalogue:SourceRoot).");
            return AppResult<ImportReport>.Fail(error);
        }

        root = Path.GetFullPath(root);
        if (!Directory.Exists(root))
        {
            return AppResult<ImportReport>.Fail(CatalogueErrors.Missing($"Source folder not found: {root}"));
        }

        var known = (await settings.GetAsync(ct)).GarmentTypes.Select(garment => garment.Key).ToHashSet();
        var warnings = new List<string>();
        var scanned = 0;
        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var images = 0;

        foreach (var directory in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(directory);
            var file = Path.Combine(directory, $"och-{name}.json");
            if (!File.Exists(file))
            {
                continue;
            }

            scanned++;
            var bytes = await File.ReadAllBytesAsync(file, ct);
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            JsonObject json;
            try
            {
                json = JsonNode.Parse(bytes)?.AsObject() ?? throw new JsonException("empty");
            }
            catch (JsonException exception)
            {
                warnings.Add($"{name}: unreadable JSON ({exception.Message})");
                continue;
            }

            var existing = await repository.FindProductAsync(name, ct);
            if (existing is not null)
            {
                var current = CatalogueJson.Read<ProductDocument>(existing.Json);
                if (!input.UpdateExisting || current.Source?.Sha256 == sha)
                {
                    unchanged++;
                    continue;
                }
            }

            var source = new ImportSource(
                json,
                name,
                Path.GetRelativePath(root, file),
                sha,
                directory);
            var (document, stored, notes) = await MapAsync(
                source,
                known,
                input.IncludeReferenceImages,
                ct);
            warnings.AddRange(notes.Select(note => $"{name}: {note}"));
            images += stored;
            var now = timeProvider.GetUtcNow();
            if (existing is null)
            {
                await repository.InsertProductAsync(
                    ProductProjections.ToRecord(
                        document,
                        1,
                        now,
                        now),
                    "Imported from workspace",
                    ct);
                created++;
            }
            else
            {
                await UpdateAsync(
                    existing,
                    document,
                    now,
                    ct);
                updated++;
            }
        }

        var report = new ImportReport(
            root,
            scanned,
            created,
            updated,
            unchanged,
            images,
            warnings);
        return AppResult<ImportReport>.Ok(report);
    }

    private async Task UpdateAsync(
        ProductRecord existing,
        ProductDocument document,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var current = CatalogueJson.Read<ProductDocument>(existing.Json);
        var merged = document with
        {
            Status = current.Status,
            SortOrder = current.SortOrder,
            Tags = current.Tags,
            Notes = current.Notes,
            Media = [.. document.Media, .. current.Media.Where(
                media => document.Media.All(next => next.Sha256 != media.Sha256))],
        };
        await repository.UpdateProductAsync(
            ProductProjections.ToRecord(
                merged,
                existing.Revision + 1,
                existing.CreatedAt,
                now),
            existing.Revision,
            "Re-imported from workspace",
            ct);
    }

    private async Task<(ProductDocument Document, int Stored, List<string> Notes)> MapAsync(
        ImportSource source,
        IReadOnlySet<string> knownTypes,
        bool includeReferences,
        CancellationToken ct)
    {
        var json = source.Json;
        var id = source.Id;
        var notes = new List<string>();
        var classification = json["classification"] as JsonObject;
        var garment = Text(classification?["garment_type"]) ?? (
            id.StartsWith("look", StringComparison.Ordinal) ? "look" : null);
        if (garment is null || !knownTypes.Contains(garment))
        {
            notes.Add($"garment type '{garment ?? "none"}' not in settings; filed as look");
            garment = "look";
        }

        var suffix = VariantSuffix().Match(id);
        var variant = Text(classification?["variant_id"]) ?? (suffix.Success ? suffix.Groups[1].Value : "v01");
        var locales = json["locales"] as JsonObject;
        var assets = json["assets"] as JsonObject;
        var mediaList = new List<ProductMedia>();
        var stored = 0;

        async Task AddAsync(string key, string role)
        {
            foreach (var path in (assets?[key] as JsonArray ?? []).Select(Text).OfType<string>())
            {
                var full = Path.GetFullPath(Path.Combine(source.Directory, path));
                var inside = full.StartsWith(source.Directory + Path.DirectorySeparatorChar, StringComparison.Ordinal);
                if (!inside || !File.Exists(full))
                {
                    notes.Add($"missing image {path}");
                    continue;
                }

                var extension = Path.GetExtension(full).ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
                {
                    continue;
                }

                var record = await media.StoreAsync(await File.ReadAllBytesAsync(full, ct), ct);
                if (mediaList.Any(item => item.Sha256 == record.Sha256))
                {
                    continue;
                }

                stored++;
                mediaList.Add(new ProductMedia(
                    record.Sha256[..12],
                    role,
                    Path.GetFileName(full),
                    record.ContentType,
                    record.ByteLength,
                    record.Sha256,
                    record.Width,
                    record.Height,
                    MediaPreview.Exists(record),
                    null,
                    path));
            }
        }

        await AddAsync("cutout_images", MediaRole.Cutout);
        await AddAsync("model_images", MediaRole.Model);
        if (includeReferences)
        {
            await AddAsync("product_reference_images", MediaRole.Reference);
        }

        await AddAsync("source_images", MediaRole.Photo);

        var pricing = Pricing(json["pricing"] as JsonObject);
        var uz = Locale(locales?["uz"]);
        var status = pricing is not null && uz.Name is not null && mediaList.Count > 0
            ? ProductStatus.Active
            : ProductStatus.Draft;
        var family = Array.IndexOf(FamilyOrder, garment);
        var number = suffix.Success && int.TryParse(suffix.Groups[1].Value.TrimStart('v'), out var parsed) ? parsed : 0;

        var document = new ProductDocument
        {
            Id = id,
            GarmentType = garment,
            VariantCode = variant,
            DesignId = Text(classification?["design_id"]),
            Status = status,
            SortOrder = (family < 0 ? FamilyOrder.Length : family) * 100 + number,
            Sizes = Strings(json["sizes"]),
            FabricCodes = Strings(classification?["fabric_codes"]),
            DecorationCodes = Strings(classification?["decoration_codes"]),
            Locales = new ProductLocales(
                uz,
                Locale(locales?["ru"]),
                Locale(locales?["en"])),
            Pricing = pricing,
            Media = mediaList,
            Issues = Issues(json),
            SourceVerbatim = Text((json["source"] as JsonObject)?["verbatim"]),
            Source = new ProductSource(
                "workspace-json",
                source.RelativePath,
                source.Sha256,
                timeProvider.GetUtcNow()),
        };
        return (document, stored, notes);
    }

    private static List<ReviewIssue> Issues(JsonObject json)
    {
        return ((json["review"] as JsonObject)?["issues"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select((issue, index) => new ReviewIssue(
                $"src-{index + 1}",
                Text(issue["field"]) ?? "general",
                Text(issue["status"]) == "resolved" ? IssueStatus.Resolved : IssueStatus.Open,
                Text(issue["question"]) ?? ""))
            .Where(issue => issue.Question.Length > 0)
            .ToList();
    }

    private static ProductLocale Locale(JsonNode? node)
    {
        var locale = node as JsonObject;
        return new ProductLocale(
            Text(locale?["status"]) == "approved" ? LocaleStatus.Approved : LocaleStatus.Draft,
            Text(locale?["name"]),
            Text(locale?["fabric"]),
            Text(locale?["decoration"]),
            Text(locale?["description"]));
    }

    private static ProductPricing? Pricing(JsonObject? node)
    {
        if (node?["tiers"] is not JsonArray tiers || tiers.Count == 0)
        {
            return null;
        }

        var list = tiers.OfType<JsonObject>()
            .Select(tier => new PriceTier(
                tier["minimum_quantity"]?.GetValue<int>() ?? 1,
                tier["maximum_quantity"]?.GetValue<int?>(),
                tier["unit_price"]?.GetValue<long>() ?? 0))
            .OrderBy(tier => tier.MinimumQuantity)
            .ToList();
        return new ProductPricing(
            Text(node["currency"]) ?? "UZS",
            Text(node["status"]) == "user_confirmed" ? PricingStatus.Confirmed : PricingStatus.Draft,
            list,
            Text(node["wording_proposal"]));
    }

    private static IReadOnlyList<string> Strings(JsonNode? node)
    {
        return (node as JsonArray ?? []).Select(Text).OfType<string>().ToList();
    }

    private static string? Text(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        return null;
    }

    [GeneratedRegex(@"-(v\d+)$")]
    private static partial Regex VariantSuffix();
}
