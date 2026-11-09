using BrandWorkspace.Application.Commerce.Services;
using BrandWorkspace.Domain.Commerce;

namespace BrandWorkspace.Infrastructure.Commerce.Services;

/// <summary>Applies <see cref="CommerceRules"/> for the brand draft's store.</summary>
public sealed class CommerceRuleSet : ICommerceRules
{
    /// <inheritdoc />
    public void Validate(CommerceSnapshot snapshot)
    {
        CommerceRules.Validate(snapshot);
    }

    /// <inheritdoc />
    public void ValidateEditedPrices(CommerceSnapshot next, CommerceSnapshot? previous)
    {
        CommerceRules.ValidateEditedPrices(next, previous);
    }

    /// <inheritdoc />
    public CommerceSnapshot RestrictEditorApprovals(CommerceSnapshot next, CommerceSnapshot? previous)
    {
        return CommerceRules.RestrictEditorApprovals(next, previous);
    }

    /// <inheritdoc />
    public bool IsHash(string? value)
    {
        return CommerceRules.Hash(value);
    }

    /// <inheritdoc />
    public bool EditionEligible(CatalogueEdition edition, CommerceSnapshot snapshot)
    {
        return CommerceRules.EditionEligible(edition, snapshot);
    }

    /// <inheritdoc />
    public bool PdfEligible(CatalogueEdition edition, CommerceSnapshot snapshot)
    {
        return CommerceRules.PdfEligible(edition, snapshot);
    }

    /// <inheritdoc />
    public IEnumerable<string> ApprovedHashes(CommerceSnapshot snapshot)
    {
        return CommerceRules.ApprovedHashes(snapshot);
    }
}
