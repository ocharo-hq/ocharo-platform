namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>
/// Represents an existing media item to reorder or relabel; its place in the list is its display order.
/// </summary>
public sealed record MediaInput(
    string Id,
    string Role,
    string? Alt,
    bool? IsActive = null,
    string? Group = null);
