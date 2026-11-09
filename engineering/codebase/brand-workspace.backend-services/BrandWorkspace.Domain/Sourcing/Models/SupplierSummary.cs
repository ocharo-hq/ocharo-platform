namespace BrandWorkspace.Domain.Sourcing.Models;

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
