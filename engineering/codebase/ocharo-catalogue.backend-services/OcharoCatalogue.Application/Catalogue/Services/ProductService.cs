using System.Text.RegularExpressions;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Product commands and queries: validation, revision-checked saves, history and bulk edits.</summary>
public sealed partial class ProductService(ICatalogueRepository repository, SettingsService settings, IMediaStore media, IClock clock)
{
    private const int MaxText = 600;
    private const int MaxDescription = 4000;

    public async Task<IReadOnlyList<ProductSummary>> ListAsync(ProductFilter filter, CancellationToken ct)
    {
        var records = await repository.GetProductsAsync(ct);
        var collections = (await repository.GetCollectionsAsync(ct)).Select(record => CatalogueJson.Read<CollectionDocument>(record.Json)).ToList();
        HashSet<string>? inCollection = null;
        if (!string.IsNullOrWhiteSpace(filter.Collection))
        {
            var collection = await repository.FindCollectionAsync(filter.Collection, ct)
                ?? throw CatalogueException.NotFound("Collection");
            inCollection = CatalogueJson.Read<CollectionDocument>(collection.Json).ProductIds.ToHashSet();
        }

        var query = filter.Query?.Trim();
        return records
            .Select(r => (Record: r, Document: CatalogueJson.Read<ProductDocument>(r.Json)))
            .Where(x => string.IsNullOrWhiteSpace(filter.GarmentType) || x.Record.GarmentType == filter.GarmentType)
            .Where(x => string.IsNullOrWhiteSpace(filter.Status) || x.Record.Status == filter.Status)
            .Where(x => inCollection is null || inCollection.Contains(x.Record.Id))
            .Where(x => string.IsNullOrWhiteSpace(filter.Tag) || x.Document.Tags.Contains(filter.Tag))
            .Where(x => string.IsNullOrEmpty(query) || Matches(x.Document, query))
            .OrderBy(x => x.Record.SortOrder).ThenBy(x => x.Record.GarmentType).ThenBy(x => x.Record.Id, StringComparer.Ordinal)
            .Select(x => Summarize(x.Document, x.Record, collections.Where(collection => collection.ProductIds.Contains(x.Record.Id)).Select(collection => collection.Id).ToList()))
            .ToList();
    }

    public async Task<ProductDetail> GetAsync(string id, CancellationToken ct)
    {
        var record = await repository.FindProductAsync(id, ct) ?? throw CatalogueException.NotFound("Product");
        return await DetailAsync(record, ct);
    }

    public async Task<ProductDetail> CreateAsync(ProductInput input, CancellationToken ct)
    {
        var current = await settings.GetAsync(ct);
        var garmentType = input.GarmentType.Trim();
        var existing = await repository.GetProductsAsync(ct);
        var variant = string.IsNullOrWhiteSpace(input.VariantCode) ? NextVariant(existing, garmentType) : input.VariantCode.Trim();
        var id = string.IsNullOrWhiteSpace(input.Id) ? $"{garmentType}-{variant}" : input.Id.Trim();
        if (existing.Any(r => r.Id == id)) throw CatalogueException.Conflict($"A product with id '{id}' already exists.");

        var document = Normalize(input with { VariantCode = variant }, id, current, [], null, null);
        var now = clock.UtcNow;
        var record = ToRecord(document, revision: 1, now, now);
        await repository.InsertProductAsync(record, Summary(input.ChangeSummary, "Created"), ct);
        return await DetailAsync(record, ct);
    }

    public async Task<ProductDetail> UpdateAsync(string id, ProductInput input, CancellationToken ct)
    {
        if (input.ExpectedRevision is not { } expected) throw new CatalogueException("Send the revision you edited (expectedRevision).");
        var record = await repository.FindProductAsync(id, ct) ?? throw CatalogueException.NotFound("Product");
        var current = CatalogueJson.Read<ProductDocument>(record.Json);
        var document = Normalize(input with { VariantCode = input.VariantCode ?? current.VariantCode }, id, await settings.GetAsync(ct),
            current.Media, current.SourceVerbatim, current.Source, current.Locales, current.Pricing);
        var saved = await repository.UpdateProductAsync(ToRecord(document, expected + 1, record.CreatedAt, clock.UtcNow),
            expected, Summary(input.ChangeSummary, Describe(current, document)), ct);
        return await DetailAsync(saved, ct);
    }

    public async Task<ProductDetail> DuplicateAsync(string id, CancellationToken ct)
    {
        var record = await repository.FindProductAsync(id, ct) ?? throw CatalogueException.NotFound("Product");
        var source = CatalogueJson.Read<ProductDocument>(record.Json);
        var existing = await repository.GetProductsAsync(ct);
        var variant = NextVariant(existing, source.GarmentType);
        var copy = source with
        {
            Id = $"{source.GarmentType}-{variant}",
            VariantCode = variant,
            Status = ProductStatus.Draft,
            Issues = [],
            Source = null,
            SourceVerbatim = null,
            Locales = new ProductLocales(Draft(source.Locales.Uz), Draft(source.Locales.Ru), Draft(source.Locales.En))
            {
                Additional = (source.Locales.Additional ?? new Dictionary<string, ProductLocale>()).ToDictionary(entry => entry.Key, entry => Draft(entry.Value), StringComparer.Ordinal),
            },
            Pricing = source.Pricing is null ? null : source.Pricing with { Status = PricingStatus.Draft },
        };
        var now = clock.UtcNow;
        var created = ToRecord(copy, 1, now, now);
        await repository.InsertProductAsync(created, $"Duplicated from {source.Id}", ct);
        return await DetailAsync(created, ct);

        static ProductLocale Draft(ProductLocale locale) => locale with { Status = LocaleStatus.Draft };
    }

    /// <summary>Stores an uploaded image and appends it to the product as a new revision.</summary>
    public async Task<ProductDetail> AddMediaAsync(string id, byte[] bytes, string fileName, string role, int? expectedRevision, CancellationToken ct)
    {
        if (!MediaRole.All.Contains(role)) throw new CatalogueException("Choose a recognised image role.");
        var record = await repository.FindProductAsync(id, ct) ?? throw CatalogueException.NotFound("Product");
        if (expectedRevision is { } expected && expected != record.Revision)
            throw CatalogueException.Conflict("The product changed since you opened it. Reload before adding images.");
        var stored = await media.StoreAsync(bytes, ct);
        var document = CatalogueJson.Read<ProductDocument>(record.Json);
        if (document.Media.Any(m => m.Sha256 == stored.Sha256))
            throw CatalogueException.Conflict("This exact image is already attached to the product.");
        var item = new ProductMedia(Guid.NewGuid().ToString("N")[..12], role, SafeFileName(fileName), stored.ContentType,
            stored.ByteLength, stored.Sha256, stored.Width, stored.Height, stored.PreviewPath is not null, null, null);
        var updated = document with { Media = [.. document.Media, item] };
        var saved = await repository.UpdateProductAsync(ToRecord(updated, record.Revision + 1, record.CreatedAt, clock.UtcNow),
            record.Revision, $"Added {role} image {item.FileName}", ct);
        return await DetailAsync(saved, ct);
    }

    public async Task<IReadOnlyList<RevisionSummary>> RevisionsAsync(string id, CancellationToken ct)
    {
        _ = await repository.FindProductAsync(id, ct) ?? throw CatalogueException.NotFound("Product");
        return (await repository.GetRevisionsAsync(id, ct))
            .OrderByDescending(r => r.Revision)
            .Select(r => new RevisionSummary(r.Revision, r.SavedAt, r.Summary))
            .ToList();
    }

    public async Task<ProductDocument> GetRevisionAsync(string id, int revision, CancellationToken ct)
    {
        var snapshot = await repository.FindRevisionAsync(id, revision, ct) ?? throw CatalogueException.NotFound("Revision");
        return CatalogueJson.Read<ProductDocument>(snapshot.Json);
    }

    /// <summary>Saves an older snapshot as the newest revision; history is never rewritten.</summary>
    public async Task<ProductDetail> RestoreAsync(string id, int revision, int expectedRevision, CancellationToken ct)
    {
        var record = await repository.FindProductAsync(id, ct) ?? throw CatalogueException.NotFound("Product");
        var snapshot = await repository.FindRevisionAsync(id, revision, ct) ?? throw CatalogueException.NotFound("Revision");
        var document = CatalogueJson.Read<ProductDocument>(snapshot.Json);
        var problem = ActivationProblem(document);
        if (problem is not null) throw new CatalogueException($"Cannot restore an active product: {problem}.");
        var saved = await repository.UpdateProductAsync(ToRecord(document, expectedRevision + 1, record.CreatedAt, clock.UtcNow),
            expectedRevision, $"Restored revision {revision}", ct);
        return await DetailAsync(saved, ct);
    }

    public async Task<BulkResult> SetStatusAsync(BulkStatusInput input, CancellationToken ct)
    {
        if (!ProductStatus.All.Contains(input.Status)) throw new CatalogueException("Unknown product status.");
        var skipped = new List<string>();
        var changed = 0;
        foreach (var id in input.ProductIds.Distinct())
        {
            var record = await repository.FindProductAsync(id, ct);
            if (record is null) { skipped.Add($"{id}: not found"); continue; }
            var document = CatalogueJson.Read<ProductDocument>(record.Json);
            if (document.Status == input.Status) continue;
            var next = document with { Status = input.Status };
            var problem = ActivationProblem(next);
            if (problem is not null) { skipped.Add($"{id}: {problem}"); continue; }
            await repository.UpdateProductAsync(ToRecord(next, record.Revision + 1, record.CreatedAt, clock.UtcNow),
                record.Revision, $"Status {document.Status} → {input.Status}", ct);
            changed++;
        }

        return new BulkResult(changed, skipped);
    }

    public async Task<BulkResult> AdjustPricesAsync(BulkPricingInput input, CancellationToken ct)
    {
        if (input.Mode is not ("percent" or "add")) throw new CatalogueException("Mode must be 'percent' or 'add'.");
        if (input.RoundTo is < 1 or > 1_000_000) throw new CatalogueException("Rounding must be between 1 and 1,000,000.");
        if (input.Mode == "percent" && input.Value is < -90 or > 500) throw new CatalogueException("Percent change must be between -90 and 500.");
        if (input.Tier is not (null or "retail" or "wholesale")) throw new CatalogueException("Tier must be retail, wholesale or empty for all.");
        var skipped = new List<string>();
        var changed = 0;
        foreach (var id in input.ProductIds.Distinct())
        {
            var record = await repository.FindProductAsync(id, ct);
            if (record is null) { skipped.Add($"{id}: not found"); continue; }
            var document = CatalogueJson.Read<ProductDocument>(record.Json);
            if (document.Pricing is not { Tiers.Count: > 0 } pricing) { skipped.Add($"{id}: no price recorded"); continue; }
            if (!ProductPublicationEligibility.StandardBands(pricing.Tiers)) { skipped.Add($"{id}: confirm the 1–9 and 10+ price bands first"); continue; }
            var bulkStart = pricing.Tiers.Max(t => t.MinimumQuantity);
            var tiers = pricing.Tiers.Select(t =>
            {
                var applies = input.Tier switch
                {
                    "retail" => t.MinimumQuantity == 1,
                    "wholesale" => t.MinimumQuantity == bulkStart && bulkStart > 1,
                    _ => true,
                };
                if (!applies) return t;
                var raw = input.Mode == "percent" ? t.UnitPrice * (100 + input.Value) / 100 : t.UnitPrice + input.Value;
                var rounded = (long)(Math.Round(raw / input.RoundTo, MidpointRounding.AwayFromZero) * input.RoundTo);
                return t with { UnitPrice = Math.Max(0, rounded) };
            }).ToList();
            if (!ProductPublicationEligibility.ValidTiers(tiers)) { skipped.Add($"{id}: adjusted price is out of range"); continue; }
            if (tiers.SequenceEqual(pricing.Tiers)) continue;
            var next = document with { Pricing = pricing with { Tiers = tiers, Status = PricingStatus.Draft } };
            await repository.UpdateProductAsync(ToRecord(next, record.Revision + 1, record.CreatedAt, clock.UtcNow),
                record.Revision, input.Mode == "percent" ? $"Prices {input.Value:+0.##;-0.##}%" : $"Prices {input.Value:+0;-0} UZS", ct);
            changed++;
        }

        return new BulkResult(changed, skipped);
    }

    /// <summary>Every product's copy for the translation grid, in catalogue order.</summary>
    public async Task<IReadOnlyList<ProductCopyRow>> CopyRowsAsync(CancellationToken ct) =>
        (await repository.GetProductsAsync(ct))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => (Record: r, Document: CatalogueJson.Read<ProductDocument>(r.Json)))
            .Select(x =>
            {
                var primary = PrimaryImage(x.Document);
                return new ProductCopyRow(x.Record.Id, x.Record.GarmentType, x.Record.Status, x.Record.Revision,
                    primary is null ? null : primary.HasPreview ? CatalogueUrls.Preview(primary.Sha256) : CatalogueUrls.Media(primary.Sha256), x.Document.Locales);
            })
            .ToList();

    /// <summary>Saves localized copy; omitted additional languages retain their current values.</summary>
    public async Task<ProductCopyRow> UpdateCopyAsync(string id, CopyInput input, CancellationToken ct)
    {
        var record = await repository.FindProductAsync(id, ct) ?? throw CatalogueException.NotFound("Product");
        var current = CatalogueJson.Read<ProductDocument>(record.Json);
        var locales = NormalizeLocales(input.Locales, current.Locales);
        var next = current with { Locales = locales };
        var problem = ActivationProblem(next);
        if (problem is not null) throw new CatalogueException($"Cannot keep the product active: {problem}.");
        var approved = CatalogueLocales.All.Count(code => locales.For(code).Status == LocaleStatus.Approved);
        var saved = await repository.UpdateProductAsync(ToRecord(next, input.ExpectedRevision + 1, record.CreatedAt, clock.UtcNow),
            input.ExpectedRevision, $"Edited copy ({approved}/{CatalogueLocales.All.Count} approved)", ct);
        var primary = PrimaryImage(next);
        return new ProductCopyRow(saved.Id, saved.GarmentType, saved.Status, saved.Revision,
            primary is null ? null : primary.HasPreview ? CatalogueUrls.Preview(primary.Sha256) : CatalogueUrls.Media(primary.Sha256), next.Locales);
    }

    /// <summary>Maps a stored product to its grid row.</summary>
    public static ProductSummary Summarize(ProductDocument d, ProductRecord r, IReadOnlyList<string>? collectionIds = null)
    {
        var primary = PrimaryImage(d);
        var languages = AuthoredLanguages(d.Locales);
        var approved = languages.Count(code => d.Locales.For(code).Status == LocaleStatus.Approved && !string.IsNullOrWhiteSpace(d.Locales.For(code).Name));
        return new ProductSummary(d.Id, d.GarmentType, d.VariantCode, d.Status, d.SortOrder, d.Locales.Uz.Name, d.Locales.Ru.Name,
            d.Locales.En.Name, d.Pricing?.RetailPrice, d.Pricing?.WholesalePrice, d.Pricing?.Status, approved,
            d.Issues.Count(i => i.Status == IssueStatus.Open), d.Media.Count,
            primary is null ? null : CatalogueUrls.Media(primary.Sha256),
            primary is null ? null : primary.HasPreview ? CatalogueUrls.Preview(primary.Sha256) : CatalogueUrls.Media(primary.Sha256),
            d.Tags, r.Revision, r.UpdatedAt, d.Availability, d.Stock?.Values.Sum(), languages.Count, languages.Count - approved, collectionIds ?? []);
    }

    /// <summary>The first cutout, else the first model image, else the first image.</summary>
    public static ProductMedia? PrimaryImage(ProductDocument d) =>
        ProductPublicationEligibility.ActiveImages(d).FirstOrDefault(m => m.Role == MediaRole.Cutout)
        ?? ProductPublicationEligibility.ActiveImages(d).FirstOrDefault(m => m.Role == MediaRole.Model)
        ?? ProductPublicationEligibility.ActiveImages(d).FirstOrDefault();

    /// <summary>Why a document cannot be active, or null when it can.</summary>
    public static string? ActivationProblem(ProductDocument d)
    {
        if (d.Status != ProductStatus.Active) return null;
        if (!CatalogueLocales.All.Any(code => !string.IsNullOrWhiteSpace(d.Locales.For(code).Name))) return "an active product needs a name";
        if (ProductPublicationEligibility.ActiveImages(d).Count == 0) return "an active product needs at least one active image";
        return null;
    }

    public static ProductRecord ToRecord(ProductDocument d, int revision, DateTimeOffset created, DateTimeOffset updated) => new()
    {
        Id = d.Id,
        GarmentType = d.GarmentType,
        Status = d.Status,
        SortOrder = d.SortOrder,
        Revision = revision,
        CreatedAt = created,
        UpdatedAt = updated,
        Json = CatalogueJson.Write(d),
    };

    public static string NextVariant(IEnumerable<ProductRecord> existing, string garmentType)
    {
        var highest = existing
            .Where(r => r.GarmentType == garmentType)
            .Select(r => VariantNumber().Match(r.Id))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value))
            .DefaultIfEmpty(0)
            .Max();
        return $"v{highest + 1:00}";
    }

    private async Task<ProductDetail> DetailAsync(ProductRecord record, CancellationToken ct)
    {
        var document = CatalogueJson.Read<ProductDocument>(record.Json);
        var collections = (await repository.GetCollectionsAsync(ct))
            .Where(c => CatalogueJson.Read<CollectionDocument>(c.Json).ProductIds.Contains(record.Id))
            .Select(c => c.Id)
            .ToList();
        var views = document.Media
            .Select(m => new MediaView(m, CatalogueUrls.Media(m.Sha256), m.HasPreview ? CatalogueUrls.Preview(m.Sha256) : null))
            .ToList();
        return new ProductDetail(document, record.Revision, record.CreatedAt, record.UpdatedAt, views, collections);
    }

    private static ProductDocument Normalize(ProductInput input, string id, CatalogueSettings settings,
        IReadOnlyList<ProductMedia> currentMedia, string? sourceVerbatim, ProductSource? source,
        ProductLocales? currentLocales = null, ProductPricing? currentPricing = null)
    {
        if (!Slug().IsMatch(id) || id.Length > 64) throw new CatalogueException("Id must be lowercase letters, digits and single hyphens (max 64).");
        var garmentType = input.GarmentType.Trim();
        if (settings.GarmentTypes.All(g => g.Key != garmentType))
            throw new CatalogueException($"Unknown garment type '{garmentType}'. Add it in settings first.");
        var variant = (input.VariantCode ?? "").Trim();
        if (!Slug().IsMatch(variant) || variant.Length > 16) throw new CatalogueException("Variant code must be a short slug such as v07.");
        if (!ProductStatus.All.Contains(input.Status)) throw new CatalogueException("Unknown product status.");

        var locales = input.Locales ?? currentLocales ?? ProductLocales.Empty();
        var document = new ProductDocument
        {
            Id = id,
            GarmentType = garmentType,
            VariantCode = variant,
            DesignId = Clean(input.DesignId, 64),
            Status = input.Status,
            SortOrder = Math.Clamp(input.SortOrder, -100_000, 100_000),
            Sizes = Codes(input.Sizes, 12, 20, "size"),
            FabricCodes = Codes(input.FabricCodes, 40, 20, "fabric code"),
            DecorationCodes = Codes(input.DecorationCodes, 40, 20, "decoration code"),
            Tags = Codes(input.Tags, 40, 20, "tag"),
            Locales = NormalizeLocales(locales, currentLocales),
            Pricing = Pricing(input.Pricing, settings.Currency, currentPricing),
            Media = Media(input.Media, currentMedia),
            Issues = Issues(input.Issues),
            SourceVerbatim = sourceVerbatim,
            Source = source,
            Notes = Clean(input.Notes, MaxDescription),
            Stock = Stock(input.Stock, Codes(input.Sizes, 12, 20, "size")),
            MadeToOrder = input.MadeToOrder,
        };
        var problem = ActivationProblem(document);
        if (problem is not null) throw new CatalogueException($"Cannot activate: {problem}.");
        return document;
    }

    private static IReadOnlyDictionary<string, int>? Stock(IReadOnlyDictionary<string, int>? stock, IReadOnlyList<string> sizes)
    {
        if (stock is null) return null;
        var unknown = stock.Keys.Where(k => !sizes.Contains(k)).ToList();
        if (unknown.Count > 0) throw new CatalogueException($"Stock names sizes the product does not offer: {string.Join(", ", unknown)}.");
        if (stock.Values.Any(v => v is < 0 or > 100_000)) throw new CatalogueException("Stock per size must be between 0 and 100,000.");
        return sizes.ToDictionary(s => s, s => stock.GetValueOrDefault(s));
    }

    /// <summary>The stock sheet: every product's sizes, pieces on hand and availability.</summary>
    public async Task<IReadOnlyList<StockRow>> StockRowsAsync(CancellationToken ct) =>
        (await repository.GetProductsAsync(ct))
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => (Record: r, Document: CatalogueJson.Read<ProductDocument>(r.Json)))
            .Select(x => StockRowOf(x.Document, x.Record.Status, x.Record.Revision))
            .ToList();

    /// <summary>Saves only stock tracking; everything else keeps its current value.</summary>
    public async Task<StockRow> UpdateStockAsync(string id, StockInput input, CancellationToken ct)
    {
        var record = await repository.FindProductAsync(id, ct) ?? throw CatalogueException.NotFound("Product");
        var current = CatalogueJson.Read<ProductDocument>(record.Json);
        var next = current with { Stock = Stock(input.Stock, current.Sizes), MadeToOrder = input.MadeToOrder };
        var summary = next.MadeToOrder ? "Stock: made to order" : next.Stock is null ? "Stock: not tracked" : $"Stock: {next.Stock.Values.Sum()} pieces";
        var saved = await repository.UpdateProductAsync(ToRecord(next, input.ExpectedRevision + 1, record.CreatedAt, clock.UtcNow), input.ExpectedRevision, summary, ct);
        return StockRowOf(next, saved.Status, saved.Revision);
    }

    private static StockRow StockRowOf(ProductDocument d, string status, int revision)
    {
        var primary = PrimaryImage(d);
        return new StockRow(d.Id, d.GarmentType, status, revision, d.Locales.Uz.Name,
            primary is null ? null : primary.HasPreview ? CatalogueUrls.Preview(primary.Sha256) : CatalogueUrls.Media(primary.Sha256),
            d.Sizes, d.Stock, d.MadeToOrder, d.Availability);
    }

    private static IReadOnlyList<string> AuthoredLanguages(ProductLocales locales) => CatalogueLocales.All.Where(code =>
        code is "uz" or "ru" or "en" || locales.Additional?.ContainsKey(code) == true &&
        (locales.For(code).Status == LocaleStatus.Approved || new[] { locales.For(code).Name, locales.For(code).Fabric, locales.For(code).Decoration, locales.For(code).Description }.Any(value => !string.IsNullOrWhiteSpace(value)))).ToList();

    private static ProductLocales NormalizeLocales(ProductLocales locales, ProductLocales? previous = null)
    {
        var additional = new Dictionary<string, ProductLocale>(StringComparer.Ordinal);
        if (previous?.Additional is { } retained)
            foreach (var entry in retained) additional[entry.Key] = entry.Value;
        foreach (var entry in locales.Additional ?? new Dictionary<string, ProductLocale>())
        {
            if (!CatalogueLocales.Supported(entry.Key) || entry.Key is "uz" or "ru" or "en")
                throw new CatalogueException($"Unsupported additional catalogue language '{entry.Key}'.");
            additional[entry.Key] = Locale(entry.Value, entry.Key);
        }
        return new ProductLocales(Locale(locales.Uz, "uz"), Locale(locales.Ru, "ru"), Locale(locales.En, "en")) { Additional = additional };
    }

    private static ProductLocale Locale(ProductLocale? locale, string code)
    {
        locale ??= ProductLocale.Empty();
        if (!LocaleStatus.All.Contains(locale.Status)) throw new CatalogueException($"Unknown {code} copy status.");
        var normalized = new ProductLocale(locale.Status, Clean(locale.Name, 120), Clean(locale.Fabric, MaxText),
            Clean(locale.Decoration, MaxText), Clean(locale.Description, MaxDescription));
        if (normalized.Status == LocaleStatus.Approved && normalized.Name is null)
            throw new CatalogueException($"Approved {code} copy needs a name.");
        return normalized;
    }

    private static ProductPricing? Pricing(ProductPricing? pricing, string currency, ProductPricing? previous = null)
    {
        if (pricing is null || pricing.Tiers.Count == 0) return null;
        if (pricing.Tiers.Count > 100) throw new CatalogueException("Too many price tiers.");
        if (!PricingStatus.All.Contains(pricing.Status)) throw new CatalogueException("Unknown pricing status.");
        if (!string.Equals(pricing.Currency, currency, StringComparison.Ordinal)) throw new CatalogueException($"Prices are recorded in {currency}.");
        var tiers = pricing.Tiers.OrderBy(t => t.MinimumQuantity).ToList();
        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            if (tier.MinimumQuantity < 1) throw new CatalogueException("A price tier starts at quantity 1 or more.");
            if (tier.MaximumQuantity is { } max && max < tier.MinimumQuantity) throw new CatalogueException("A tier's maximum is below its minimum.");
            if (tier.UnitPrice is < 0 or > 1_000_000_000) throw new CatalogueException("Unit price is out of range.");
            if (i < tiers.Count - 1)
            {
                var next = tiers[i + 1];
                if (tier.MaximumQuantity is null || tier.MaximumQuantity >= next.MinimumQuantity)
                    throw new CatalogueException("Price tiers overlap; only the last tier may be open-ended.");
            }
        }

        if (!ProductPublicationEligibility.StandardBands(tiers) && (previous is null || !SamePricing(previous, pricing)))
            throw new CatalogueException("Use the two price bands: 1–9 pieces and 10+ pieces.");
        return new ProductPricing(currency, pricing.Status, tiers, Clean(pricing.Wording, MaxText));
    }

    private static IReadOnlyList<ProductMedia> Media(IReadOnlyList<MediaInput>? input, IReadOnlyList<ProductMedia> current)
    {
        if (input is null) return current;
        var byId = current.ToDictionary(m => m.Id);
        var result = new List<ProductMedia>();
        foreach (var item in input)
        {
            if (!byId.TryGetValue(item.Id, out var existing)) throw new CatalogueException("Upload images before referencing them.");
            if (!MediaRole.All.Contains(item.Role)) throw new CatalogueException("Choose a recognised image role.");
            if (result.Any(m => m.Id == item.Id)) throw new CatalogueException("An image is listed twice.");
            var group = item.Group is null ? existing.Group : Clean(item.Group, 30);
            if (group is not null && !ProductMediaGroup.All.Contains(group)) throw new CatalogueException("Choose a recognised image group.");
            result.Add(existing with { Role = item.Role, Alt = Clean(item.Alt, 300), IsActive = item.IsActive ?? existing.IsActive, Group = group });
        }

        return result;
    }

    private static IReadOnlyList<ReviewIssue> Issues(IReadOnlyList<ReviewIssue>? issues) =>
        (issues ?? []).Select(i =>
        {
            if (!IssueStatus.All.Contains(i.Status)) throw new CatalogueException("Unknown review status.");
            var question = Clean(i.Question, 2000) ?? throw new CatalogueException("A review note needs text.");
            return new ReviewIssue(string.IsNullOrWhiteSpace(i.Id) ? Guid.NewGuid().ToString("N")[..10] : i.Id.Trim(),
                Clean(i.Field, 120) ?? "general", i.Status, question);
        }).ToList();

    private static IReadOnlyList<string> Codes(IReadOnlyList<string>? values, int maxLength, int maxCount, string what)
    {
        var list = (values ?? []).Select(v => v.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (list.Count > maxCount) throw new CatalogueException($"Too many {what}s (max {maxCount}).");
        if (list.Any(v => v.Length > maxLength)) throw new CatalogueException($"A {what} is longer than {maxLength} characters.");
        return list;
    }

    private static string? Clean(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > max) throw new CatalogueException($"Text is longer than {max} characters.");
        return trimmed;
    }

    private static bool Matches(ProductDocument d, string query) =>
        new[] { d.Id, d.Notes }
            .Concat(CatalogueLocales.All.SelectMany(code => new[] { d.Locales.For(code).Name, d.Locales.For(code).Fabric }))
            .Concat(d.Tags)
            .Any(v => v?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);

    private static string Describe(ProductDocument before, ProductDocument after)
    {
        var changes = new List<string>();
        if (before.Status != after.Status) changes.Add($"status {before.Status} → {after.Status}");
        if (before.Locales != after.Locales) changes.Add("copy");
        if (!SamePricing(before.Pricing, after.Pricing)) changes.Add("pricing");
        if (!before.Sizes.SequenceEqual(after.Sizes)) changes.Add("sizes");
        if (!before.Media.SequenceEqual(after.Media)) changes.Add("images");
        if (!before.Issues.SequenceEqual(after.Issues)) changes.Add("review notes");
        if (!before.Tags.SequenceEqual(after.Tags)) changes.Add("tags");
        return changes.Count == 0 ? "Saved" : "Edited " + string.Join(", ", changes);
    }

    private static bool SamePricing(ProductPricing? a, ProductPricing? b) =>
        a is null || b is null
            ? a is null && b is null
            : a.Currency == b.Currency && a.Status == b.Status && a.Wording == b.Wording && a.Tiers.SequenceEqual(b.Tiers);

    private static string Summary(string? requested, string fallback) =>
        string.IsNullOrWhiteSpace(requested) ? fallback : requested.Trim()[..Math.Min(200, requested.Trim().Length)];

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(name) || name.Length > 150 ? "image" : name;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Slug();

    [GeneratedRegex(@"-v(\d+)$")]
    private static partial Regex VariantNumber();
}
