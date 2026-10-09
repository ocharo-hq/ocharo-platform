using BrandWorkspace.Application.Sso;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace BrandWorkspace.Api.Sso;

/// <summary>Internal delegation, not an OAuth/OIDC issuer. Uses existing revocable Desk authority.</summary>
public sealed class DeskSsoService(bool enabled, IReadOnlyDictionary<string, DeskSsoClient> clients,
    ISsoGrantStore store, IDeskSsoAuthority authority, TimeProvider clock)
{
    public DeskSsoClient? FindClient(string? id) => enabled && id is { Length: >= 1 and <= 80 }
        && clients.TryGetValue(id, out var client) && client.IsValid ? client : null;

    public async Task<string?> AuthorizeAsync(string clientId, string redirectUri, string challenge,
        SsoGrant grant, CancellationToken ct = default)
    {
        var client = FindClient(clientId);
        var now = clock.GetUtcNow();
        if (client is null || client.RedirectUri != redirectUri || !IsChallenge(challenge)
            || grant.ExpiresAt > now.AddHours(8) || !authority.IsTicketSigningConfigured
            || await MemberAsync(client, grant, ct) is null) return null;
        var code = Token();
        await store.StoreCodeAsync(new(Hash(code), clientId, redirectUri, challenge, grant,
            now.AddSeconds(60) < grant.ExpiresAt ? now.AddSeconds(60) : grant.ExpiresAt), ct);
        return code;
    }

    public async Task<SsoTokenResult?> ExchangeAsync(string clientId, string secret, string redirectUri,
        string code, string verifier, CancellationToken ct = default)
    {
        var client = Authenticate(clientId, secret);
        if (client is null || client.RedirectUri != redirectUri || !IsToken(code) || !IsVerifier(verifier)
            || !authority.IsTicketSigningConfigured) return null;
        var token = Token();
        var grant = await store.ConsumeCodeAsync(Hash(code), clientId, redirectUri, Hash(verifier), Hash(token), clock.GetUtcNow(), ct);
        var member = grant is null ? null : await MemberAsync(client, grant, ct);
        if (member is null)
        {
            if (grant is not null) await store.RevokeAsync(Hash(token), clientId, clock.GetUtcNow(), ct);
            return null;
        }
        return Issue(client, grant!, member, token);
    }

    public async Task<SsoTokenResult?> RefreshAsync(string clientId, string secret, string token, CancellationToken ct = default)
    {
        var client = Authenticate(clientId, secret);
        if (client is null || !IsToken(token) || !authority.IsTicketSigningConfigured) return null;
        var hash = Hash(token);
        var grant = await store.FindGrantAsync(hash, clientId, clock.GetUtcNow(), ct);
        var member = grant is null ? null : await MemberAsync(client, grant, ct);
        if (member is null) return null;
        var next = Token();
        var rotated = await store.RotateAsync(hash, clientId, Hash(next), clock.GetUtcNow(), ct);
        return rotated is null ? null : Issue(client, rotated, member, next);
    }

    public async Task<bool> RevokeAsync(string clientId, string secret, string token, CancellationToken ct = default)
    {
        if (Authenticate(clientId, secret) is null || !IsToken(token)) return false;
        await store.RevokeAsync(Hash(token), clientId, clock.GetUtcNow(), ct);
        return true;
    }

    private DeskSsoClient? Authenticate(string id, string secret)
    {
        var client = FindClient(id);
        return client?.Authenticate(secret) == true ? client : null;
    }

    private Task<SsoMembership?> MemberAsync(DeskSsoClient client, SsoGrant grant, CancellationToken ct) =>
        client.OrganizationId != grant.OrganizationId || grant.ExpiresAt <= clock.GetUtcNow()
            ? Task.FromResult<SsoMembership?>(null) : authority.GetMembershipAsync(grant, clock.GetUtcNow(), ct);

    private SsoTokenResult Issue(DeskSsoClient client, SsoGrant grant, SsoMembership member, string refresh)
    {
        var issued = authority.IssueTicket(client.Audience, member, grant.SessionId);
        return new(issued.Token, issued.ExpiresAt, refresh, grant.ExpiresAt);
    }

    public static string Hash(string value) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(value)));
    public static bool IsChallenge(string? value) => value is { Length: 43 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static bool IsToken(string? value) => IsChallenge(value);
    public static bool IsVerifier(string? value) => value is { Length: >= 43 and <= 128 } && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~');
    public static string Token() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}
