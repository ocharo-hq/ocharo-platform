namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>Validated, normalized supplier fields.</summary>
public sealed record SupplierInput(
    string Name,
    string Stage,
    string? Country,
    string? City,
    string? Website,
    string Notes);
