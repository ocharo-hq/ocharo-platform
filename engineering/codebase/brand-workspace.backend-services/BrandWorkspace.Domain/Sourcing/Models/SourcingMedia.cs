namespace BrandWorkspace.Domain.Sourcing.Models;

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
