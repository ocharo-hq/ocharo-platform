namespace BrandWorkspace.Application.Sso;

public sealed record SsoMembership(Guid OrganizationId, string UserId, string Role, bool CanEdit, bool CanApprove);
public sealed record SsoGrant(string UserId, Guid SessionId, Guid OrganizationId, DateTimeOffset ExpiresAt);
public sealed record SsoAuthorizationCode(string Hash, string ClientId, string RedirectUri, string Challenge, SsoGrant SsoGrant, DateTimeOffset ExpiresAt);
public sealed record SsoAccessTicket(string Token, DateTimeOffset ExpiresAt);
public sealed record SsoTokenResult(string Ticket, DateTimeOffset ExpiresAt, string RefreshToken, DateTimeOffset SessionExpiresAt);

public interface IDeskSsoAuthority
{
    Task<SsoMembership?> GetMembershipAsync(SsoGrant grant, DateTimeOffset now, CancellationToken ct);
    bool IsTicketSigningConfigured { get; }
    SsoAccessTicket IssueTicket(string audience, SsoMembership membership, Guid sessionId);
}

public interface ISsoGrantStore
{
    Task StoreCodeAsync(SsoAuthorizationCode code, CancellationToken ct);
    Task<SsoGrant?> ConsumeCodeAsync(string hash, string client, string redirect, string challenge, string newHash, DateTimeOffset now, CancellationToken ct);
    Task<SsoGrant?> FindGrantAsync(string hash, string client, DateTimeOffset now, CancellationToken ct);
    Task<SsoGrant?> RotateAsync(string hash, string client, string newHash, DateTimeOffset now, CancellationToken ct);
    Task RevokeAsync(string hash, string client, DateTimeOffset now, CancellationToken ct);
}
