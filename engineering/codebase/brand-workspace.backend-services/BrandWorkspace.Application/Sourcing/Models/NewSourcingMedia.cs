namespace BrandWorkspace.Application.Sourcing.Models;

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
