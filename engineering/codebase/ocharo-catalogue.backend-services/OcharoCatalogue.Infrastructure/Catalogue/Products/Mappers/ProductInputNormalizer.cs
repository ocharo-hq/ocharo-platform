using System.Text.RegularExpressions;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Policies;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;

/// <summary>Maps product input to a normalized product document; input that breaks a catalogue rule fails as invalid
/// through the mediator's exception bridge.</summary>
public static partial class ProductInputNormalizer
{
    private const int MaxText = 600;
    private const int MaxDescription = 4000;

    /// <summary>Maps <paramref name="input"/> to the document saved under <paramref name="id"/>.</summary>
    /// <param name="input">The product fields.</param>
    /// <param name="id">The product id.</param>
    /// <param name="settings">The catalogue settings.</param>
    /// <param name="current">The parts of the stored document the input cannot replace.</param>
    public static ProductDocument ToDocument(
        ProductInput input,
        string id,
        CatalogueSettings settings,
        ProductCurrentParts current)
    {
        if (!Slug().IsMatch(id) || id.Length > 64)
        {
            throw Invalid("Id must be lowercase letters, digits and single hyphens (max 64).");
        }

        var garmentType = input.GarmentType.Trim();
        if (settings.GarmentTypes.All(garment => garment.Key != garmentType))
        {
            throw Invalid($"Unknown garment type '{garmentType}'. Add it in settings first.");
        }

        var variant = (input.VariantCode ?? "").Trim();
        if (!Slug().IsMatch(variant) || variant.Length > 16)
        {
            throw Invalid("Variant code must be a short slug such as v07.");
        }

        if (!ProductStatus.All.Contains(input.Status))
        {
            throw Invalid("Unknown product status.");
        }

        var locales = input.Locales ?? current.Locales ?? ProductLocales.Empty();
        var sizes = Codes(
            input.Sizes,
            12,
            20,
            "size");
        var document = new ProductDocument
        {
            Id = id,
            GarmentType = garmentType,
            VariantCode = variant,
            DesignId = Clean(input.DesignId, 64),
            Status = input.Status,
            SortOrder = Math.Clamp(
                input.SortOrder,
                -100_000,
                100_000),
            Sizes = Codes(
                input.Sizes,
                12,
                20,
                "size"),
            FabricCodes = Codes(
                input.FabricCodes,
                40,
                20,
                "fabric code"),
            DecorationCodes = Codes(
                input.DecorationCodes,
                40,
                20,
                "decoration code"),
            Tags = Codes(
                input.Tags,
                40,
                20,
                "tag"),
            Locales = Locales(locales, current.Locales),
            Pricing = Pricing(
                input.Pricing,
                settings.Currency,
                current.Pricing),
            Media = Media(input.Media, current.Media),
            Issues = Issues(input.Issues),
            SourceVerbatim = current.SourceVerbatim,
            Source = current.Source,
            Notes = Clean(input.Notes, MaxDescription),
            Stock = Stock(input.Stock, sizes),
            MadeToOrder = input.MadeToOrder,
        };
        if (ProductActivationPolicy.Problem(document) is { } problem)
        {
            throw Invalid($"Cannot activate: {problem}.");
        }

        return document;
    }

    /// <summary>Maps localized copy to its normalized form; additional languages <paramref name="previous"/> held stay
    /// unless replaced.</summary>
    /// <param name="locales">The copy the caller sent.</param>
    /// <param name="previous">The stored copy; null for a new product.</param>
    public static ProductLocales Locales(ProductLocales locales, ProductLocales? previous = null)
    {
        var additional = new Dictionary<string, ProductLocale>(StringComparer.Ordinal);
        if (previous?.Additional is { } retained)
        {
            foreach (var entry in retained)
            {
                additional[entry.Key] = entry.Value;
            }
        }

        foreach (var entry in locales.Additional ?? new Dictionary<string, ProductLocale>())
        {
            if (!CatalogueLocales.Supported(entry.Key) || entry.Key is "uz" or "ru" or "en")
            {
                throw Invalid($"Unsupported additional catalogue language '{entry.Key}'.");
            }

            additional[entry.Key] = Locale(entry.Value, entry.Key);
        }

        return new ProductLocales(
            Locale(locales.Uz, "uz"),
            Locale(locales.Ru, "ru"),
            Locale(locales.En, "en"))
        {
            Additional = additional,
        };
    }

    /// <summary>Maps stock per size to every offered size, or refuses sizes the product does not offer.</summary>
    /// <param name="stock">The pieces on hand per size; null stops tracking.</param>
    /// <param name="sizes">The offered sizes.</param>
    public static IReadOnlyDictionary<string, int>? Stock(
        IReadOnlyDictionary<string, int>? stock,
        IReadOnlyList<string> sizes)
    {
        if (stock is null)
        {
            return null;
        }

        var unknown = stock.Keys.Where(key => !sizes.Contains(key)).ToList();
        if (unknown.Count > 0)
        {
            throw Invalid($"Stock names sizes the product does not offer: {string.Join(", ", unknown)}.");
        }

        if (stock.Values.Any(value => value is < 0 or > 100_000))
        {
            throw Invalid("Stock per size must be between 0 and 100,000.");
        }

        return sizes.ToDictionary(size => size, size => stock.GetValueOrDefault(size));
    }

    /// <summary>Tells whether two prices are the same sheet: currency, status, wording and tiers.</summary>
    /// <param name="a">One price sheet.</param>
    /// <param name="b">The other price sheet.</param>
    public static bool SamePricing(ProductPricing? a, ProductPricing? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        return a.Currency == b.Currency && a.Status == b.Status && a.Wording == b.Wording && a.Tiers.SequenceEqual(
            b.Tiers);
    }

    private static ProductLocale Locale(ProductLocale? locale, string code)
    {
        locale ??= ProductLocale.Empty();
        if (!LocaleStatus.All.Contains(locale.Status))
        {
            throw Invalid($"Unknown {code} copy status.");
        }

        var normalized = new ProductLocale(
            locale.Status,
            Clean(locale.Name, 120),
            Clean(locale.Fabric, MaxText),
            Clean(locale.Decoration, MaxText),
            Clean(locale.Description, MaxDescription));
        if (normalized.Status == LocaleStatus.Approved && normalized.Name is null)
        {
            throw Invalid($"Approved {code} copy needs a name.");
        }

        return normalized;
    }

    private static ProductPricing? Pricing(
        ProductPricing? pricing,
        string currency,
        ProductPricing? previous)
    {
        if (pricing is null || pricing.Tiers.Count == 0)
        {
            return null;
        }

        if (pricing.Tiers.Count > 100)
        {
            throw Invalid("Too many price tiers.");
        }

        if (!PricingStatus.All.Contains(pricing.Status))
        {
            throw Invalid("Unknown pricing status.");
        }

        if (!string.Equals(
            pricing.Currency,
            currency,
            StringComparison.Ordinal))
        {
            throw Invalid($"Prices are recorded in {currency}.");
        }

        var tiers = pricing.Tiers.OrderBy(tier => tier.MinimumQuantity).ToList();
        for (var i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            if (tier.MinimumQuantity < 1)
            {
                throw Invalid("A price tier starts at quantity 1 or more.");
            }

            if (tier.MaximumQuantity is { } max && max < tier.MinimumQuantity)
            {
                throw Invalid("A tier's maximum is below its minimum.");
            }

            if (tier.UnitPrice is < 0 or > 1_000_000_000)
            {
                throw Invalid("Unit price is out of range.");
            }

            if (i < tiers.Count - 1)
            {
                var next = tiers[i + 1];
                if (tier.MaximumQuantity is null || tier.MaximumQuantity >= next.MinimumQuantity)
                {
                    throw Invalid("Price tiers overlap; only the last tier may be open-ended.");
                }
            }
        }

        if (!ProductPublicationEligibility.StandardBands(tiers)
            && (previous is null || !SamePricing(previous, pricing)))
        {
            throw Invalid("Use the two price bands: 1–9 pieces and 10+ pieces.");
        }

        return new ProductPricing(
            currency,
            pricing.Status,
            tiers,
            Clean(pricing.Wording, MaxText));
    }

    private static IReadOnlyList<ProductMedia> Media(
        IReadOnlyList<MediaInput>? input,
        IReadOnlyList<ProductMedia> current)
    {
        if (input is null)
        {
            return current;
        }

        var byId = current.ToDictionary(media => media.Id);
        var result = new List<ProductMedia>();
        foreach (var item in input)
        {
            if (!byId.TryGetValue(item.Id, out var existing))
            {
                throw Invalid("Upload images before referencing them.");
            }

            if (!MediaRole.All.Contains(item.Role))
            {
                throw Invalid("Choose a recognised image role.");
            }

            if (result.Any(media => media.Id == item.Id))
            {
                throw Invalid("An image is listed twice.");
            }

            var group = item.Group is null ? existing.Group : Clean(item.Group, 30);
            if (group is not null && !ProductMediaGroup.All.Contains(group))
            {
                throw Invalid("Choose a recognised image group.");
            }

            result.Add(existing with
            {
                Role = item.Role,
                Alt = Clean(item.Alt, 300),
                IsActive = item.IsActive ?? existing.IsActive,
                Group = group,
            });
        }

        return result;
    }

    private static IReadOnlyList<ReviewIssue> Issues(IReadOnlyList<ReviewIssue>? issues)
    {
        return (issues ?? []).Select(Issue).ToList();
    }

    private static ReviewIssue Issue(ReviewIssue issue)
    {
        if (!IssueStatus.All.Contains(issue.Status))
        {
            throw Invalid("Unknown review status.");
        }

        var question = Clean(issue.Question, 2000) ?? throw Invalid("A review note needs text.");
        var id = string.IsNullOrWhiteSpace(issue.Id) ? Guid.NewGuid().ToString("N")[..10] : issue.Id.Trim();
        return new ReviewIssue(
            id,
            Clean(issue.Field, 120) ?? "general",
            issue.Status,
            question);
    }

    private static IReadOnlyList<string> Codes(
        IReadOnlyList<string>? values,
        int maxLength,
        int maxCount,
        string what)
    {
        var list = (values ?? [])
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (list.Count > maxCount)
        {
            throw Invalid($"Too many {what}s (max {maxCount}).");
        }

        if (list.Any(value => value.Length > maxLength))
        {
            throw Invalid($"A {what} is longer than {maxLength} characters.");
        }

        return list;
    }

    private static string? Clean(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > max)
        {
            throw Invalid($"Text is longer than {max} characters.");
        }

        return trimmed;
    }

    private static Exception Invalid(string message)
    {
        return CatalogueErrors.Invalid(message).ToException();
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Slug();
}
