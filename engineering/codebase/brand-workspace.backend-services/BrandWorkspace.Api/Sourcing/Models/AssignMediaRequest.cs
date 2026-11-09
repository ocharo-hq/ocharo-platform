namespace BrandWorkspace.Api.Sourcing.Models;

/// <summary>Represents media to move to a supplier, or back to the inbox.</summary>
public sealed record AssignMediaRequest(IReadOnlyList<Guid>? MediaIds, Guid? SupplierId);
