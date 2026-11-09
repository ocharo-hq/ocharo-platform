namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents the product grid's filters.</summary>
public sealed record ProductFilter(
    string? Query,
    string? GarmentType,
    string? Status,
    string? Collection,
    string? Tag);
