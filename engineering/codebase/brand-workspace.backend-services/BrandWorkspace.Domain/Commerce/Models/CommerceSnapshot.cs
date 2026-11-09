using OcharoBrand.Domain.Brand;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>
/// Represents a brand's complete private commerce draft; source identifiers and original domain documents stay intact.
/// </summary>
public sealed record CommerceSnapshot
{
    public required string Slug { get; init; }
    public required BrandIdentity Identity { get; init; }
    public IReadOnlyDictionary<string, LogoVersion> Logos { get; init; } = new Dictionary<string, LogoVersion>();
    public IReadOnlyList<CommerceProduct> Products { get; init; } = [];
    public IReadOnlyList<CollectionDocument> Collections { get; init; } = [];
    public IReadOnlyDictionary<string, MediaApproval> MediaApprovals { get; init; } =
        new Dictionary<string, MediaApproval>();
    public IReadOnlyList<string> VerifiedLinkKeys { get; init; } = [];
    public IReadOnlyList<CatalogueEdition> CatalogueEditions { get; init; } = [];
    public IReadOnlyList<SourceEvidence> Sources { get; init; } = [];
}
