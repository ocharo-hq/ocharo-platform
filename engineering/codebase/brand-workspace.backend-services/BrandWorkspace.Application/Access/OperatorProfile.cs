namespace BrandWorkspace.Application.Access;

/// <summary>An operator's current Desk profile; either value may be absent.</summary>
public sealed record OperatorProfile(string? DisplayName, string? Email);
