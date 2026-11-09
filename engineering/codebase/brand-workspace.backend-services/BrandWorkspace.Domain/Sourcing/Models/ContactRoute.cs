namespace BrandWorkspace.Domain.Sourcing.Models;

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
