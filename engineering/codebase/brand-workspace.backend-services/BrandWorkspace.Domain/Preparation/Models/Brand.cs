namespace BrandWorkspace.Domain.Preparation.Models;

/// <summary>An organization-owned brand; its timezone is configuration, not a schedule.</summary>
public sealed record Brand(Guid Id, Guid OrganizationId, string Name, string TimeZone);
