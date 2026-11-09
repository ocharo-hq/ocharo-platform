using BrandWorkspace.Domain.Commerce;

namespace BrandWorkspace.Application.Commerce.Models;

/// <summary>Represents the catalogue metadata one saved design revision carries.</summary>
public sealed record CatalogueDefinitionMetadata(string Slug, string Description, bool IsActive, bool WebEnabled, bool PdfEnabled, CatalogueWebVersion Web);
