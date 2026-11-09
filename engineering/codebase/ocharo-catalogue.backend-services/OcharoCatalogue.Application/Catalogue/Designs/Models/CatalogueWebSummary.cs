namespace OcharoCatalogue.Application.Catalogue.Designs.Models;

/// <summary>Represents a design's web output in the gallery: its theme and active page count.</summary>
public sealed record CatalogueWebSummary(string Theme, int PageCount);
