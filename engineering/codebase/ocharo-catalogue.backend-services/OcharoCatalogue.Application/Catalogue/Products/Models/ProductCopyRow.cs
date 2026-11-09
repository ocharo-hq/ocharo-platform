using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents one row of the copy grid: every language's copy for a product.</summary>
public sealed record ProductCopyRow(
    string Id,
    string GarmentType,
    string Status,
    int Revision,
    string? PreviewUrl,
    ProductLocales Locales);
