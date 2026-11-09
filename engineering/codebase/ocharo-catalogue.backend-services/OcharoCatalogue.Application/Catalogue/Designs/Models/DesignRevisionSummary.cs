namespace OcharoCatalogue.Application.Catalogue.Designs.Models;

/// <summary>Represents a saved design revision in the history list.</summary>
/// <param name="Id">The design id.</param>
/// <param name="Revision">The revision number.</param>
/// <param name="Title">The design title at that revision.</param>
/// <param name="SavedAtUtc">When the revision was saved.</param>
public sealed record DesignRevisionSummary(
    Guid Id,
    int Revision,
    string Title,
    DateTimeOffset SavedAtUtc);
