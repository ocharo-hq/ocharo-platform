namespace BrandWorkspace.Domain.Sourcing.Models;

/// <summary>A material supplier with the people and contact routes that reach it.</summary>
public sealed record Supplier(
    Guid Id,
    string? Key,
    string Name,
    string Stage,
    string? Country,
    string? City,
    string? Website,
    string Notes,
    IReadOnlyList<SupplierPerson> People,
    IReadOnlyList<ContactRoute> Routes,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
