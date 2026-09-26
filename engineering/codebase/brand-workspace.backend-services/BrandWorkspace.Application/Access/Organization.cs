namespace BrandWorkspace.Application.Access;

/// <summary>An organization visible to the authenticated member.</summary>
public sealed record Organization(Guid Id, string Name, string Slug, string Role);
