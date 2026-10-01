using OcharoBrand.Domain.Brand;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace BrandWorkspace.Domain.Commerce;

/// <summary>The complete private draft. Source identifiers and original domain documents stay intact.</summary>
public sealed record CommerceSnapshot
{
    public required string Slug { get; init; }
    public required BrandIdentity Identity { get; init; }
    public IReadOnlyDictionary<string, LogoVersion> Logos { get; init; } = new Dictionary<string, LogoVersion>();
    public IReadOnlyList<CommerceProduct> Products { get; init; } = [];
    public IReadOnlyList<CollectionDocument> Collections { get; init; } = [];
    public IReadOnlyDictionary<string, MediaApproval> MediaApprovals { get; init; } = new Dictionary<string, MediaApproval>();
    public IReadOnlyList<string> VerifiedLinkKeys { get; init; } = [];
    public IReadOnlyList<CatalogueEdition> CatalogueEditions { get; init; } = [];
    public IReadOnlyList<SourceEvidence> Sources { get; init; } = [];
}
public sealed record CommerceProduct(ProductDocument Document, int Revision = 1, int? SourceRevision = null);
public sealed record MediaApproval(string Kind, bool ApprovedForPublication, string? SourceUrl = null, string? Credit = null, string? Rights = null);
public sealed record SourceEvidence(string Kind, string Id, int Revision, string Sha256);
public sealed record CommerceDraft(Guid BrandId, int Revision, CommerceSnapshot Snapshot, DateTimeOffset UpdatedAt);
public sealed record CommerceRelease(Guid Id, Guid BrandId, int Revision, int SourceRevision, DateTimeOffset PublishedAt,
    string Note, CommerceSnapshot Snapshot, DateTimeOffset? WithdrawnAt = null);
public sealed record CommerceBlob(string Sha256, string ContentType, byte[] Bytes);
public sealed record CreativeSource(string Application, string WorkspaceId, string LookId, int Revision);
public sealed record CreativeAssetRelease(string Id, string Sha256);
public sealed record CreativeMaterial(string Slot, string Source, string? MaterialId, string? Sha256);
public sealed record CreativeDerivative(Guid MediaId, string Sha256, string MediaType);
public sealed record CreativeSourceApproval(string Status, string? Note);
public sealed record CreativeImportInput(Guid BrandId, string? ProductId, CreativeSource Source,
    IReadOnlyList<CreativeAssetRelease> AssetReleases, IReadOnlyList<CreativeMaterial> Materials,
    CreativeDerivative Derivative, CreativeSourceApproval? SourceApproval);
public sealed record CreativeImport(Guid Id, DateTimeOffset CreatedAt, string Status, CreativeImportInput Input);
public sealed record ImportedCommerce(CommerceSnapshot Snapshot, IReadOnlyList<CommerceBlob> Blobs);

/// <summary>Backend-owned artifacts from one saved immutable editor revision; approval covers these exact bytes.</summary>
public sealed record CatalogueEdition(Guid Id, Guid SourceDesignId, int SourceRevision, string Title, string Locale,
    string TemplateId, IReadOnlyList<CatalogueEditionPage> Pages, string PdfSha256, bool ApprovedForPublication, DateTimeOffset CreatedAt);
public sealed record CatalogueEditionPage(string Id, string Title, double WidthMm, double HeightMm,
    IReadOnlyList<string> ProductIds, string PngSha256, string PdfSha256);
