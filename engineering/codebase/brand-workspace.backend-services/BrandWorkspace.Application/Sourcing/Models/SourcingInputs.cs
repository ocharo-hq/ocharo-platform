namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>Validated, normalized supplier fields.</summary>
public sealed record SupplierInput(string Name, string Stage, string? Country, string? City, string? Website, string Notes);

/// <summary>Validated, normalized person fields.</summary>
public sealed record PersonInput(string Name, string? Role, string Notes);

/// <summary>Validated, normalized contact route fields; the person must belong to the same supplier.</summary>
public sealed record RouteInput(
    string Channel,
    string Destination,
    string? Label,
    string? SourceUrl,
    bool Verified,
    bool IsPrimary,
    Guid? PersonId,
    string Notes);

/// <summary>Validated mutable media fields; a null supplier returns the item to the inbox.</summary>
public sealed record MediaUpdateInput(Guid? SupplierId, string Capture, string Caption);

/// <summary>Inspected bytes plus the operator-supplied description of one media item.</summary>
public sealed record NewSourcingMedia(
    Guid? SupplierId,
    InspectedMedia Content,
    string Capture,
    string OriginalName,
    string Caption,
    string? SourceLabel,
    string? SourceMessageId,
    DateTimeOffset? ReceivedAt);

/// <summary>Original bytes with the facts derived from them on the server.</summary>
public sealed record InspectedMedia(
    byte[] Bytes,
    string Sha256,
    string ContentType,
    int? Width,
    int? Height,
    byte[]? Thumbnail,
    DateTimeOffset? TakenAt,
    bool HasLocation,
    long? PerceptualHash);

/// <summary>Stored original bytes ready to serve.</summary>
public sealed record SourcingMediaContent(byte[] Bytes, string ContentType, string OriginalName);
