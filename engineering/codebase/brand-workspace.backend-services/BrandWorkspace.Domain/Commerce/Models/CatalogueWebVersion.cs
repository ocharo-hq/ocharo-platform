namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents the web version of a catalogue edition: its theme, pages and display choices.</summary>
public sealed record CatalogueWebVersion(string Theme, IReadOnlyList<CatalogueWebPage> Pages)
{
    public string MediaGroup { get; init; } = "all";
    public CataloguePriceDisplay PriceDisplay { get; init; } = new(true, true, true, true, true);
    public IReadOnlyDictionary<string, string> Copy { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ProductMedia { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();
}
