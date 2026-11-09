namespace BrandWorkspace.Api.Sourcing.Models;

/// <summary>Represents a supplier's replacement fields at the revision the operator edited.</summary>
public sealed record UpdateSupplierRequest(
    int ExpectedRevision,
    string? Name,
    string? Stage,
    string? Country,
    string? City,
    string? Website,
    string? Notes);
