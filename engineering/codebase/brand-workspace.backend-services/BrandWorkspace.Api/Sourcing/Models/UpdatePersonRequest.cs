namespace BrandWorkspace.Api.Sourcing.Models;

/// <summary>Represents a person's replacement fields at the revision the operator edited.</summary>
public sealed record UpdatePersonRequest(int ExpectedRevision, string? Name, string? Role, string? Notes);
