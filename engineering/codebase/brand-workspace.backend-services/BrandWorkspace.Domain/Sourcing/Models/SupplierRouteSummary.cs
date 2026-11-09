namespace BrandWorkspace.Domain.Sourcing.Models;

/// <summary>The channel and destination of a supplier's primary route.</summary>
public sealed record SupplierRouteSummary(string Channel, string Destination);
