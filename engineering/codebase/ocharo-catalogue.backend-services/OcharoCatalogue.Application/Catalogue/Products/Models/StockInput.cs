namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents a revision-checked stock change; a null <see cref="Stock"/> stops tracking.</summary>
public sealed record StockInput(
    int ExpectedRevision,
    IReadOnlyDictionary<string, int>? Stock,
    bool MadeToOrder);
