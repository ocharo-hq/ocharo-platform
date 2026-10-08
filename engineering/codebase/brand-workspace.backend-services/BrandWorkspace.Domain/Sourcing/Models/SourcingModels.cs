namespace BrandWorkspace.Domain.Sourcing.Models;

/// <summary>A material supplier with the people and contact routes that reach it.</summary>
public sealed record Supplier(
    Guid Id,
    string? Key,
    string Name,
    string Stage,
    string? Country,
    string? City,
    string? Website,
    string Notes,
    IReadOnlyList<SupplierPerson> People,
    IReadOnlyList<ContactRoute> Routes,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A supplier row for lists, with counts and its newest previewable image.</summary>
public sealed record SupplierSummary(
    Guid Id,
    string? Key,
    string Name,
    string Stage,
    string? Country,
    string? City,
    SupplierRouteSummary? PrimaryRoute,
    int RouteCount,
    int PersonCount,
    int MediaCount,
    Guid? CoverMediaId,
    int Revision,
    DateTimeOffset UpdatedAt);

/// <summary>The channel and destination of a supplier's primary route.</summary>
public sealed record SupplierRouteSummary(string Channel, string Destination);

/// <summary>A named person who speaks for a supplier.</summary>
public sealed record SupplierPerson(
    Guid Id,
    Guid SupplierId,
    string Name,
    string? Role,
    string Notes,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>One way to reach a supplier; the same destination may serve several suppliers.</summary>
public sealed record ContactRoute(
    Guid Id,
    Guid SupplierId,
    Guid? PersonId,
    string Channel,
    string Destination,
    string? Label,
    string? SourceUrl,
    bool Verified,
    bool IsPrimary,
    string Notes,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Immutable content-addressed media a supplier shared; a null supplier means the inbox.</summary>
public sealed record SourcingMedia(
    Guid Id,
    Guid? SupplierId,
    string Sha256,
    string ContentType,
    int ByteSize,
    int? Width,
    int? Height,
    bool HasThumbnail,
    string Capture,
    string OriginalName,
    string Caption,
    string? SourceLabel,
    string? SourceMessageId,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset? TakenAt,
    bool HasLocation,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Organization-wide media totals for the inbox badge.</summary>
public sealed record SourcingMediaTotals(int Total, int Unassigned);

/// <summary>The outcome of storing one upload: a new item or the existing one with the same bytes.</summary>
public sealed record SourcingMediaUpload(SourcingMedia Media, bool Duplicate, IReadOnlyList<Guid> NearDuplicateIds);
