namespace BrandWorkspace.Api.Sourcing.Models;

/// <summary>Represents a new contact route as the operator sent it.</summary>
public sealed record RouteRequest(string? Channel, string? Destination, string? Label, string? SourceUrl, bool Verified, bool IsPrimary, Guid? PersonId, string? Notes);
