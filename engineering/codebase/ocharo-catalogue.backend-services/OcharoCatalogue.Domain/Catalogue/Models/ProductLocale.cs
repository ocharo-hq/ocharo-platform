namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Copy for one language; <see cref="Status"/> is draft until the owner approves the wording.</summary>
public sealed record ProductLocale(string Status, string? Name, string? Fabric, string? Decoration, string? Description)
{
    /// <summary>An empty draft locale.</summary>
    public static ProductLocale Empty() => new(LocaleStatus.Draft, null, null, null, null);
}
