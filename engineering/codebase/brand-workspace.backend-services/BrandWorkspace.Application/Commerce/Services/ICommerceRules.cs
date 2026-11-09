using BrandWorkspace.Domain.Commerce;

namespace BrandWorkspace.Application.Commerce.Services;

/// <summary>Defines the commerce rules a brand draft's store applies inside its transactions.</summary>
public interface ICommerceRules
{
    /// <summary>Refuses a draft outside its bounds or with private material marked for publication.</summary>
    /// <param name="snapshot">The draft to check.</param>
    void Validate(CommerceSnapshot snapshot);

    /// <summary>Refuses an edited price that leaves the two UZS tiers.</summary>
    /// <param name="next">The draft being saved.</param>
    /// <param name="previous">The stored draft, if any.</param>
    void ValidateEditedPrices(CommerceSnapshot next, CommerceSnapshot? previous);

    /// <summary>Returns <paramref name="next"/> with every approval an editor changed reset to a draft.</summary>
    /// <param name="next">The draft being saved.</param>
    /// <param name="previous">The stored draft, if any.</param>
    CommerceSnapshot RestrictEditorApprovals(CommerceSnapshot next, CommerceSnapshot? previous);

    /// <summary>Tells whether <paramref name="value"/> is a lowercase SHA-256.</summary>
    /// <param name="value">The value to check.</param>
    bool IsHash(string? value);

    /// <summary>Tells whether an edition has an active output whose products may be published.</summary>
    /// <param name="edition">The edition to check.</param>
    /// <param name="snapshot">The draft holding its products.</param>
    bool EditionEligible(CatalogueEdition edition, CommerceSnapshot snapshot);

    /// <summary>Tells whether an edition's PDF may be published.</summary>
    /// <param name="edition">The edition to check.</param>
    /// <param name="snapshot">The draft holding its products.</param>
    bool PdfEligible(CatalogueEdition edition, CommerceSnapshot snapshot);

    /// <summary>Lists the media hashes a release may serve publicly.</summary>
    /// <param name="snapshot">The released draft.</param>
    IEnumerable<string> ApprovedHashes(CommerceSnapshot snapshot);
}
