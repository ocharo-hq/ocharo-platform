namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>Validated mutable media fields; a null supplier returns the item to the inbox.</summary>
public sealed record MediaUpdateInput(Guid? SupplierId, string Capture, string Caption);
