using OcharoCatalogue.Domain.Catalogue.Models;

namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>
/// Represents a commerce product: canonical details plus additional translations and reusable media selections.
/// </summary>
public sealed record CommerceProduct(ProductDocument Document, int Revision = 1, int? SourceRevision = null)
{
    /// <summary>Additional languages; Uzbek, Russian and English stay in the original document.</summary>
    public IReadOnlyDictionary<string, ProductLocale>? Translations { get; init; }

    /// <summary>Named photo sets whose members refer to this product's attached media.</summary>
    public IReadOnlyList<ProductMediaGroup>? MediaGroups { get; init; }
}
