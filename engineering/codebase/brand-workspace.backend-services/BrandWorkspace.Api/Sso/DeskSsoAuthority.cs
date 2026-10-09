using BrandWorkspace.Api.Integrations;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Sso;

namespace BrandWorkspace.Api.Sso;

/// <summary>Reuses live Desk session/member authority and the existing delegated ticket issuer.</summary>
public sealed class DeskSsoAuthority(IAccessRepository access, IntegrationTickets tickets) : IDeskSsoAuthority
{
    public bool IsTicketSigningConfigured => tickets.IsConfigured;

    public async Task<SsoMembership?> GetMembershipAsync(SsoGrant grant, DateTimeOffset now, CancellationToken ct)
    {
        if (!await access.IsSessionActiveAsync(grant.SessionId, grant.UserId, now, ct)) return null;
        var member = await access.FindMembershipAsync(grant.OrganizationId, grant.UserId, ct);
        return member is null ? null : new(member.OrganizationId, member.UserId, member.Role, member.CanEdit, member.CanApprove);
    }

    public SsoAccessTicket IssueTicket(string audience, SsoMembership member, Guid sessionId)
    {
        var ticket = tickets.Issue(audience, new(member.OrganizationId, member.UserId, member.Role, member.CanEdit, member.CanApprove), sessionId)
            ?? throw new InvalidOperationException("Delegation signing is unavailable.");
        return new(ticket.Token, ticket.ExpiresAt);
    }
}
