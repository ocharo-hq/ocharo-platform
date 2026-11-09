namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Product lifecycle: only <see cref="Active"/> products reach the public feed.</summary>
public static class ProductStatus
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Draft, Active, Archived };
}
