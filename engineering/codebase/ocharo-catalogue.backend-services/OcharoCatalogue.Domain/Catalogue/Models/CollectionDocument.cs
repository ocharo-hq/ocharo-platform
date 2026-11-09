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
