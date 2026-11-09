namespace BrandWorkspace.Api.Sourcing.Models;

/// <summary>Represents a new supplier person as the operator sent it.</summary>
public sealed record PersonRequest(string? Name, string? Role, string? Notes);
