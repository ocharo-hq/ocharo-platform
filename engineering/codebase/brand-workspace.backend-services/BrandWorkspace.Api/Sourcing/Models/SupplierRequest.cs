namespace BrandWorkspace.Api.Sourcing.Models;

/// <summary>Represents a new supplier as the operator sent it.</summary>
public sealed record SupplierRequest(
    string? Name,
    string? Stage,
    string? Country,
    string? City,
    string? Website,
    string? Notes);
