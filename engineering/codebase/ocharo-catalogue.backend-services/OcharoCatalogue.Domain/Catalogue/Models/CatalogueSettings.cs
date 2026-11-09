namespace OcharoCatalogue.Domain.Catalogue.Models;

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
