namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>An ordered, named selection of products (a season, a lookbook, a merchant sheet).</summary>
public sealed record CollectionDocument
{
    /// <summary>Stable slug.</summary>
    public required string Id { get; init; }

    /// <summary>Localized collection names.</summary>
    public required CollectionNames Names { get; init; }

    /// <summary>Optional editorial description (Uzbek first).</summary>
    public string? Description { get; init; }

    /// <summary>Product ids in display order.</summary>
    public IReadOnlyList<string> ProductIds { get; init; } = [];

    /// <summary>Whether the collection appears in the public feed.</summary>
    public bool IsPublished { get; init; }

    /// <summary>Manual order among collections.</summary>
    public int SortOrder { get; init; }
}

/// <summary>Collection names per catalogue language.</summary>
public sealed record CollectionNames(string Uz, string? Ru, string? En)
{
    /// <summary>Returns the name for a locale, falling back to Uzbek.</summary>
    public string For(string? locale) => locale switch
    {
        "ru" when !string.IsNullOrWhiteSpace(Ru) => Ru!,
        "en" when !string.IsNullOrWhiteSpace(En) => En!,
        _ => Uz,
    };
}

/// <summary>Brand-level catalogue settings: garment families, size presets and print contacts.</summary>
public sealed record CatalogueSettings
{
    public required string BrandName { get; init; }
    public string Currency { get; init; } = "UZS";
    public IReadOnlyList<GarmentTypeDefinition> GarmentTypes { get; init; } = [];
    public IReadOnlyList<string> SizePreset { get; init; } = [];
    public IReadOnlyList<string> ContactLines { get; init; } = [];
    public string? PriceNote { get; init; }

    /// <summary>The Ocharo defaults used when no settings row exists yet.</summary>
    public static CatalogueSettings Default() => new()
    {
        BrandName = "Ocharo",
        GarmentTypes =
        [
            new("nimcha", "Nimcha", "Жилет", "Vest"),
            new("baska", "Baska", "Баска", "Peplum"),
            new("korset", "Korset", "Корсет", "Corset"),
            new("kostyum", "Kostyum", "Жакет-кейп", "Cropped cape jacket"),
            new("abaya", "Abaya", "Абая", "Abaya"),
            new("look", "Obraz", "Образ", "Look"),
        ],
        SizePreset = ["S", "M", "L", "XL", "XXXL"],
        // Actual account handles keep their exact spelling (brand guidelines).
        ContactLines = ["Instagram: ocharostile", "Telegram: Ocharo_Style"],
        PriceNote = "Narx buyurtma miqdoriga qarab belgilanadi.",
    };
}

/// <summary>A garment family with its names in the three catalogue languages.</summary>
public sealed record GarmentTypeDefinition(string Key, string Uz, string Ru, string En)
{
    /// <summary>Returns the family name for a locale.</summary>
    public string For(string? locale) => locale switch { "ru" => Ru, "en" => En, _ => Uz };
}
