namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents one page of a web catalogue and the products it shows.</summary>
public sealed record CatalogueWebPage(string Id, string Title, string Layout, IReadOnlyList<string> ProductIds)
{
    public bool IsActive { get; init; } = true;
}
