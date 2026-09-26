namespace BrandWorkspace.Application.Access;

/// <summary>Trusted organization membership resolved for the current request.</summary>
public sealed record OrganizationAccess(Guid OrganizationId, string UserId, string Role, bool CanEdit, bool CanApprove);
