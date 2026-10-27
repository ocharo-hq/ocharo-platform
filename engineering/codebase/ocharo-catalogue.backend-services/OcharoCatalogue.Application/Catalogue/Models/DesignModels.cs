using System.Text.Json;
namespace OcharoCatalogue.Application.Catalogue.Models;

public sealed record CatalogueDesignInput(string Title, JsonElement Snapshot, int? ExpectedRevision = null);
public sealed record CatalogueDesignDetail(Guid Id, string Title, int Revision, bool Archived, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, JsonElement Snapshot);
public sealed record CatalogueDesignSummary(Guid Id, string Title, int Revision, bool Archived, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string Locale, CatalogueDesignSummaryMetadata Catalogue);
public sealed record CatalogueDesignSummaryMetadata(string Slug, string Description, bool IsActive, CatalogueOutputs Outputs, CatalogueWebSummary Web, int PdfPageCount);
public sealed record CatalogueWebSummary(string Theme, int PageCount);
public sealed record CatalogueOutputs(bool Web, bool Pdf);
public sealed record CatalogueWebPage(string Id, string Title, string Layout, bool IsActive, IReadOnlyList<string> ProductIds);
public sealed record CatalogueDefinition(string Slug, string Description, bool IsActive, CatalogueOutputs Outputs, string Theme, IReadOnlyList<CatalogueWebPage> WebPages);
public sealed record CatalogueReleasePage(string Id, string Title, double WidthMm, double HeightMm, IReadOnlyList<string> ProductIds, string PngAsset, string PdfAsset);
public sealed record CataloguePreparedRelease(Guid Id, Guid DesignId, int DesignRevision, string Slug, string Title, string Description, string Locale,
    DateTimeOffset CreatedAtUtc, JsonElement? Web, IReadOnlyList<PublicProduct> Products, IReadOnlyList<CatalogueReleasePage> Pages,
    string? PdfAsset, IReadOnlyDictionary<string,string> Assets);
public sealed record CataloguePublicAsset(string FileName, string Sha256, string ContentType, byte[] Bytes);
public sealed record CatalogueReleaseSummary(Guid Id, Guid DesignId, int DesignRevision, string Slug, string Locale, DateTimeOffset CreatedAtUtc, DateTimeOffset? PublishedAtUtc, DateTimeOffset? WithdrawnAtUtc);
