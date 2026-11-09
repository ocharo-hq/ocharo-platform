namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>Validated, normalized person fields.</summary>
public sealed record PersonInput(string Name, string? Role, string Notes);
