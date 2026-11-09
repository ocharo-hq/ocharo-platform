using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents a media item with its original and preview URLs.</summary>
public sealed record MediaView(
    ProductMedia Media,
    string Url,
    string? PreviewUrl);
