namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents one row of the stock sheet.</summary>
public sealed record StockRow(
    string Id,
    string GarmentType,
    string Status,
    int Revision,
    string? NameUz,
    string? PreviewUrl,
    IReadOnlyList<string> Sizes,
    IReadOnlyDictionary<string, int>? Stock,
    bool MadeToOrder,
    string Availability);
