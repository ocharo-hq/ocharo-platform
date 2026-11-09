namespace BrandWorkspace.Domain.Sourcing.Models;

/// <summary>A named person who speaks for a supplier.</summary>
public sealed record SupplierPerson(
    Guid Id,
    Guid SupplierId,
    string Name,
    string? Role,
    string Notes,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
