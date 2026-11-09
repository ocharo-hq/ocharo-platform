namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>
/// Represents a catalogue edition: backend-owned artifacts from one saved editor revision, approved as these exact
/// bytes.
/// </summary>
public sealed record CatalogueEdition(
    Guid Id,
    Guid SourceDesignId,
    int SourceRevision,
    string Title,
    string Locale,
    string TemplateId,
    IReadOnlyList<CatalogueEditionPage> Pages,
    string? PdfSha256,
    bool ApprovedForPublication,
    DateTimeOffset CreatedAt)
{
    public string? Slug { get; init; }
    public string Description { get; init; } = "";
    public bool IsActive { get; init; } = true;
    public CatalogueWebVersion? Web { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}
