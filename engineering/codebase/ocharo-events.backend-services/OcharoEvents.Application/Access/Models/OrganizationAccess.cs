namespace OcharoEvents.Application.Access.Models;

/// <summary>Trusted organization membership resolved for the current request.</summary>
/// <param name="OrganizationId">The route's organization, which the session's active organization matched.</param>
/// <param name="UserId">The signed-in person's subject.</param>
/// <param name="CanTrack">Whether the person holds <c>events:track</c> there, so they may propose and track events.</param>
public sealed record OrganizationAccess(Guid OrganizationId, string UserId, bool CanTrack);
