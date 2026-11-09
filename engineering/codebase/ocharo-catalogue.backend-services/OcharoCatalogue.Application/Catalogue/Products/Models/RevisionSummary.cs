namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents a saved revision in the history list.</summary>
public sealed record RevisionSummary(
    int Revision,
    DateTimeOffset SavedAt,
    string Summary);
