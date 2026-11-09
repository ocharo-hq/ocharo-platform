namespace BrandWorkspace.Api.Sourcing.Models;

/// <summary>Represents a route's replacement fields at the revision the operator edited.</summary>
public sealed record UpdateRouteRequest(int ExpectedRevision, string? Channel, string? Destination, string? Label, string? SourceUrl, bool Verified, bool IsPrimary,
    Guid? PersonId, string? Notes);
