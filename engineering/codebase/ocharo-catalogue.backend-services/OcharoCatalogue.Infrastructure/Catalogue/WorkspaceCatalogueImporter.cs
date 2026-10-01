using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue;

/// <summary>
/// Imports the workspace product records (<c>{id}/och-{id}.json</c>, schema 1.x) with their images.
/// Read-only against the source: hashes are recorded as provenance and nothing is written back.
/// </summary>
public sealed partial class WorkspaceCatalogueImporter(
    ICatalogueRepository repository,
    IMediaStore media,
    SettingsService settings,
    IOptions<CatalogueOptions> options,
    IClock clock)
{
    private static readonly string[] FamilyOrder = ["nimcha", "baska", "korset", "kostyum", "abaya", "look"];

    public async Task<ImportReport> ImportAsync(ImportInput input, CancellationToken ct)
    {
        var root = input.SourceRoot ?? options.Value.SourceRoot
            ?? throw new CatalogueException("No source folder is configured (Catalogue:SourceRoot).");
        root = Path.GetFullPath(root);
        if (!Directory.Exists(root)) throw new CatalogueException($"Source folder not found: {root}", 404);

        var known = (await settings.GetAsync(ct)).GarmentTypes.Select(g => g.Key).ToHashSet();
        var warnings = new List<string>();
        int scanned = 0, created = 0, updated = 0, unchanged = 0, images = 0;

        foreach (var directory in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(directory);
            var file = Path.Combine(directory, $"och-{name}.json");
            if (!File.Exists(file)) continue;
            scanned++;
            var bytes = await File.ReadAllBytesAsync(file, ct);
            var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            JsonObject json;
            try { json = JsonNode.Parse(bytes)?.AsObject() ?? throw new JsonException("empty"); }
            catch (JsonException exception) { warnings.Add($"{name}: unreadable JSON ({exception.Message})"); continue; }

            var existing = await repository.FindProductAsync(name, ct);
            if (existing is not null)
            {
                var current = CatalogueJson.Read<ProductDocument>(existing.Json);
                if (!input.UpdateExisting || current.Source?.Sha256 == sha) { unchanged++; continue; }
            }

            var (document, stored, notes) = await MapAsync(json, name, Path.GetRelativePath(root, file), sha, directory, known, input.IncludeReferenceImages, ct);
            warnings.AddRange(notes.Select(n => $"{name}: {n}"));
            images += stored;
            var now = clock.UtcNow;
            if (existing is null)
            {
                await repository.InsertProductAsync(ProductService.ToRecord(document, 1, now, now), "Imported from workspace", ct);
                created++;
            }
            else
            {
                var current = CatalogueJson.Read<ProductDocument>(existing.Json);
                var merged = document with
                {
                    Status = current.Status,
                    SortOrder = current.SortOrder,
                    Tags = current.Tags,
                    Notes = current.Notes,
                    Media = [.. document.Media, .. current.Media.Where(m => document.Media.All(n => n.Sha256 != m.Sha256))],
                };
                await repository.UpdateProductAsync(ProductService.ToRecord(merged, existing.Revision + 1, existing.CreatedAt, now),
                    existing.Revision, "Re-imported from workspace", ct);
                updated++;
            }
        }

        return new ImportReport(root, scanned, created, updated, unchanged, images, warnings);
    }

    private async Task<(ProductDocument Document, int Stored, List<string> Notes)> MapAsync(JsonObject json, string id, string relativePath,
        string sha, string directory, IReadOnlySet<string> knownTypes, bool includeReferences, CancellationToken ct)
    {
        var notes = new List<string>();
        var classification = json["classification"] as JsonObject;
        var garment = Text(classification?["garment_type"]) ?? (id.StartsWith("look", StringComparison.Ordinal) ? "look" : null);
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
                var full = Path.GetFullPath(Path.Combine(directory, path));
                if (!full.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(full)) { notes.Add($"missing image {path}"); continue; }
                var ext = Path.GetExtension(full).ToLowerInvariant();
                if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp")) continue;
                var record = await media.StoreAsync(await File.ReadAllBytesAsync(full, ct), ct);
                if (mediaList.Any(x => x.Sha256 == record.Sha256)) continue;
                stored++;
                mediaList.Add(new ProductMedia(record.Sha256[..12], role, Path.GetFileName(full), record.ContentType, record.ByteLength,
                    record.Sha256, record.Width, record.Height, record.PreviewPath is not null, null, path));
            }
        }

        await AddAsync("cutout_images", MediaRole.Cutout);
        await AddAsync("model_images", MediaRole.Model);
        if (includeReferences) await AddAsync("product_reference_images", MediaRole.Reference);
        await AddAsync("source_images", MediaRole.Photo);

        var pricing = Pricing(json["pricing"] as JsonObject);
        var uz = Locale(locales?["uz"]);
        var status = pricing is not null && uz.Name is not null && mediaList.Count > 0 ? ProductStatus.Active : ProductStatus.Draft;
        var family = Array.IndexOf(FamilyOrder, garment);
        var number = suffix.Success && int.TryParse(suffix.Groups[1].Value.TrimStart('v'), out var n) ? n : 0;

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
            Locales = new ProductLocales(uz, Locale(locales?["ru"]), Locale(locales?["en"])),
            Pricing = pricing,
            Media = mediaList,
            Issues = ((json["review"] as JsonObject)?["issues"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select((issue, index) => new ReviewIssue($"src-{index + 1}", Text(issue["field"]) ?? "general",
                    Text(issue["status"]) == "resolved" ? IssueStatus.Resolved : IssueStatus.Open, Text(issue["question"]) ?? ""))
                .Where(i => i.Question.Length > 0)
                .ToList(),
            SourceVerbatim = Text((json["source"] as JsonObject)?["verbatim"]),
            Source = new ProductSource("workspace-json", relativePath, sha, clock.UtcNow),
        };
        return (document, stored, notes);
    }

    private static ProductLocale Locale(JsonNode? node)
    {
        var o = node as JsonObject;
        return new ProductLocale(Text(o?["status"]) == "approved" ? LocaleStatus.Approved : LocaleStatus.Draft,
            Text(o?["name"]), Text(o?["fabric"]), Text(o?["decoration"]), Text(o?["description"]));
    }

    private static ProductPricing? Pricing(JsonObject? node)
    {
        if (node?["tiers"] is not JsonArray tiers || tiers.Count == 0) return null;
        var list = tiers.OfType<JsonObject>()
            .Select(t => new PriceTier(t["minimum_quantity"]?.GetValue<int>() ?? 1, t["maximum_quantity"]?.GetValue<int?>(), t["unit_price"]?.GetValue<long>() ?? 0))
            .OrderBy(t => t.MinimumQuantity)
            .ToList();
        return new ProductPricing(Text(node["currency"]) ?? "UZS", Text(node["status"]) == "user_confirmed" ? PricingStatus.Confirmed : PricingStatus.Draft,
            list, Text(node["wording_proposal"]));
    }

    private static IReadOnlyList<string> Strings(JsonNode? node) =>
        (node as JsonArray ?? []).Select(Text).OfType<string>().ToList();

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    [GeneratedRegex(@"-(v\d+)$")]
    private static partial Regex VariantSuffix();
}
