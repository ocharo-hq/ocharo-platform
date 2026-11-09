namespace BrandWorkspace.Application.Sourcing.Models;

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
