using BrandWorkspace.Application.Sso;
using System.Security.Cryptography;
using System.Text;
using BrandWorkspace.Api.Sso;
using Xunit;

namespace BrandWorkspace.Tests.E2E.Access;

/// <summary>Adversarial delegation-core contracts, independent of live credentials and application activation.</summary>
public sealed class SsoCoreContractTests
{
    [Fact]
    public async Task Delegation_ShouldEnforceClientSessionAndReplayBoundaries()
    {
var tests = new (string Name, Func<Task> Run)[]
{
    ("valid code exchanges once and stores only digests", async () => {
        var f = new Fixture(); var code = await f.SsoAuthorizationCode(); Assert(code is not null); Assert(f.Store.Codes.ContainsKey(DeskSsoService.Hash(code!))); Assert(!f.Store.Codes.ContainsKey(code!));
        var result = await f.Exchange(code!); Assert(result is not null); Assert(result!.SessionExpiresAt == f.SsoGrant.ExpiresAt); Assert(result.ExpiresAt < result.SessionExpiresAt);
        Assert(await f.Exchange(code!) is null); Assert(!f.Store.Grants.ContainsKey(result.RefreshToken));
    }),
    ("disabled service rejects registered clients", async () => {var f = new Fixture(false); Assert(await f.SsoAuthorizationCode() is null); }),
    ("registration rejects insecure callbacks, missing organization, weak secret hashes and MCP", () => {
        var f = new Fixture(); foreach (var bad in new[] { "http://app.test/api/sso/callback", "https://user@app.test/api/sso/callback", "https://app.test/api/sso/callback?extra=yes", "https://app.test/api/sso/callback#fragment", "https://app.test/wrong" }) Assert(!(f.Client with { RedirectUri = bad }).IsValid);
        Assert(!(f.Client with { OrganizationId = Guid.Empty }).IsValid); Assert(!(f.Client with { Audience = "ocharo-desk-mcp" }).IsValid); Assert(!(f.Client with { SecretSha256 = "abcd" }).IsValid); return Task.CompletedTask;
    }),
    ("wrong secret, callback and verifier never redeem code", async () => {
        var f = new Fixture(); var code = (await f.SsoAuthorizationCode())!;
        Assert(await f.Service.ExchangeAsync(f.Client.Id, "wrong", f.Client.RedirectUri, code, f.Verifier) is null);
        Assert(await f.Service.ExchangeAsync(f.Client.Id, f.Secret, "https://evil.test/api/sso/callback", code, f.Verifier) is null);
        Assert(await f.Service.ExchangeAsync(f.Client.Id, f.Secret, f.Client.RedirectUri, code, new string('q',43)) is null);
        Assert(await f.Exchange(code) is not null);
    }),
    ("code is bound to confidential client", async () => {
        var f = new Fixture(); var code = (await f.SsoAuthorizationCode())!; var other = f.Client with { Id = "brand" }; f.Clients[other.Id] = other;
        Assert(await f.Service.ExchangeAsync(other.Id, f.Secret, other.RedirectUri, code, f.Verifier) is null); Assert(await f.Exchange(code) is not null);
    }),
    ("code rejects exact expiry boundary", async () => {var f = new Fixture(); var code = (await f.SsoAuthorizationCode())!; f.Clock.Advance(TimeSpan.FromSeconds(60)); Assert(await f.Exchange(code) is null);}),
    ("authorization requires live session and organization membership", async () => {
        var f = new Fixture(); f.Authority.Active = false; Assert(await f.SsoAuthorizationCode() is null); f.Authority.Active = true; f.Authority.Member = false; Assert(await f.SsoAuthorizationCode() is null);
    }),
    ("code exchange observes revocation after authorization", async () => {var f = new Fixture(); var code = (await f.SsoAuthorizationCode())!; f.Authority.Active = false; Assert(await f.Exchange(code) is null); Assert(f.Store.Grants.Count == 0);}),
    ("code exchange cannot switch configured organization", async () => {var f = new Fixture(); var code = (await f.SsoAuthorizationCode())!; f.Clients[f.Client.Id] = f.Client with { OrganizationId = Guid.NewGuid() }; Assert(await f.Exchange(code) is null);}),
    ("refresh rotates atomically and preserves absolute session expiry", async () => {
        var f = new Fixture(); var a = (await f.Exchange((await f.SsoAuthorizationCode())!))!; f.Clock.Advance(TimeSpan.FromMinutes(4)); var b = await f.Service.RefreshAsync(f.Client.Id, f.Secret, a.RefreshToken);
        Assert(b is not null && b.RefreshToken != a.RefreshToken && b.SessionExpiresAt == a.SessionExpiresAt); Assert(await f.Service.RefreshAsync(f.Client.Id, f.Secret, a.RefreshToken) is null);
    }),
    ("refresh requires current membership and current session", async () => {
        var f = new Fixture(); var a = (await f.Exchange((await f.SsoAuthorizationCode())!))!; f.Authority.Member = false; Assert(await f.Service.RefreshAsync(f.Client.Id, f.Secret, a.RefreshToken) is null);
        f.Authority.Member = true; f.Authority.Active = false; Assert(await f.Service.RefreshAsync(f.Client.Id, f.Secret, a.RefreshToken) is null);
    }),
    ("refresh enforces original expiry boundary", async () => {var f = new Fixture(); var a = (await f.Exchange((await f.SsoAuthorizationCode())!))!; f.Clock.Advance(TimeSpan.FromHours(8)); Assert(await f.Service.RefreshAsync(f.Client.Id, f.Secret, a.RefreshToken) is null);}),
    ("parallel code exchanges produce exactly one grant", async () => {var f = new Fixture(); var code = (await f.SsoAuthorizationCode())!; var results = await Task.WhenAll(Enumerable.Range(0,16).Select(_ => Task.Run(() => f.Exchange(code)))); Assert(results.Count(x => x is not null) == 1);}),
    ("parallel refreshes produce exactly one replacement", async () => {var f = new Fixture(); var a = (await f.Exchange((await f.SsoAuthorizationCode())!))!; var results = await Task.WhenAll(Enumerable.Range(0,16).Select(_ => Task.Run(() => f.Service.RefreshAsync(f.Client.Id,f.Secret,a.RefreshToken)))); Assert(results.Count(x => x is not null) == 1);}),
    ("local revoke rejects future refreshes", async () => {var f = new Fixture(); var a = (await f.Exchange((await f.SsoAuthorizationCode())!))!; Assert(await f.Service.RevokeAsync(f.Client.Id,f.Secret,a.RefreshToken)); Assert(await f.Service.RefreshAsync(f.Client.Id,f.Secret,a.RefreshToken) is null);}),
    ("permissions are reloaded when delegated tickets renew", async () => {var f = new Fixture(); var a = (await f.Exchange((await f.SsoAuthorizationCode())!))!; f.Authority.CanEdit = false; Assert(await f.Service.RefreshAsync(f.Client.Id,f.Secret,a.RefreshToken) is not null); Assert(f.Authority.LastMember?.CanEdit == false);}),
    ("invalid PKCE and random malformed inputs fail closed", async () => {
        var f = new Fixture(); foreach (var value in new[] { "", "a", new string('a',129), new string('é',43), new string('/',43), new string('a',42) }) Assert(!DeskSsoService.IsVerifier(value));
        Assert(await f.Service.AuthorizeAsync(f.Client.Id,f.Client.RedirectUri,new string('a',42),f.SsoGrant) is null); Assert(await f.Service.RefreshAsync(f.Client.Id,f.Secret,"malformed") is null);
    }),
    ("authority outage is not successful authentication", async () => {var f = new Fixture(); f.Authority.Fail = true; try {await f.SsoAuthorizationCode(); throw new Exception("Expected fail-closed exception");} catch (IOException) {}}),
};

        foreach (var test in tests)
        {
            try { await test.Run(); }
            catch (Exception exception) { throw new InvalidOperationException(test.Name, exception); }
        }
    }

    private static void Assert(bool condition)
    {
        if (!condition) throw new InvalidOperationException("SSO contract assertion failed.");
    }
sealed class Clock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.Parse("2026-10-09T12:00:00Z");
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan time) => now += time;
}
sealed class Fixture
{
    public string Secret {get;} = "fixture-only-secret-32-characters-long";
    public string Verifier {get;} = new('v',64);
    public Clock Clock {get;} = new();
    public MemoryStore Store {get;} = new();
    public Authority Authority {get;}
    public DeskSsoClient Client {get;}
    public Dictionary<string,DeskSsoClient> Clients {get;}
    public DeskSsoService Service {get;}
    public SsoGrant SsoGrant {get;}
    public Fixture(bool enabled = true)
    {
        var org = Guid.NewGuid(); SsoGrant = new("test-user",Guid.NewGuid(),org,Clock.GetUtcNow().AddHours(8));
        Client = new("catalogue","https://catalogue.example.test/api/sso/callback","ocharo-catalogue",org,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Secret))));
        Clients = new() {{Client.Id,Client}}; Authority = new(Clock); Service = new(enabled,Clients,Store,Authority,Clock);
    }
    public Task<string?> SsoAuthorizationCode() => Service.AuthorizeAsync(Client.Id,Client.RedirectUri,DeskSsoService.Hash(Verifier),SsoGrant);
    public Task<SsoTokenResult?> Exchange(string code) => Service.ExchangeAsync(Client.Id,Secret,Client.RedirectUri,code,Verifier);
}
sealed class Authority(Clock clock) : IDeskSsoAuthority
{
    public bool Active {get;set;} = true; public bool Member {get;set;} = true; public bool Fail {get;set;}
    public bool CanEdit {get;set;} = true; public SsoMembership? LastMember {get;private set;}
    public bool IsTicketSigningConfigured => true;
    public Task<SsoMembership?> GetMembershipAsync(SsoGrant grant,DateTimeOffset now,CancellationToken ct)
    {
        if(Fail) throw new IOException("Simulated unavailable authority");
        return Task.FromResult(Active && Member && grant.ExpiresAt > now ? new SsoMembership(grant.OrganizationId,grant.UserId,CanEdit ? "editor":"reader",CanEdit,false):null);
    }
    public SsoAccessTicket IssueTicket(string audience,SsoMembership member,Guid sessionId) {LastMember=member;return new("fixture-ticket",clock.GetUtcNow().AddMinutes(5));}
}
sealed class MemoryStore : ISsoGrantStore
{
    private readonly object gate = new();
    public Dictionary<string,SsoAuthorizationCode> Codes {get;} = new();
    public Dictionary<string,(string Client,SsoGrant SsoGrant)> Grants {get;} = new();
    public Task StoreCodeAsync(SsoAuthorizationCode code,CancellationToken ct) {lock(gate) Codes.Add(code.Hash,code);return Task.CompletedTask;}
    public Task<SsoGrant?> ConsumeCodeAsync(string hash,string client,string redirect,string challenge,string newHash,DateTimeOffset now,CancellationToken ct)
    {
        lock(gate) {if(!Codes.TryGetValue(hash,out var c) || c.ClientId!=client || c.RedirectUri!=redirect || c.Challenge!=challenge || c.ExpiresAt<=now || c.SsoGrant.ExpiresAt<=now) return Task.FromResult<SsoGrant?>(null);
        Codes.Remove(hash);Grants.Add(newHash,(client,c.SsoGrant));return Task.FromResult<SsoGrant?>(c.SsoGrant);}
    }
    public Task<SsoGrant?> FindGrantAsync(string hash,string client,DateTimeOffset now,CancellationToken ct)
    {lock(gate) return Task.FromResult(Grants.TryGetValue(hash,out var g) && g.Client==client && g.SsoGrant.ExpiresAt>now ? g.SsoGrant:null);}
    public Task<SsoGrant?> RotateAsync(string hash,string client,string newHash,DateTimeOffset now,CancellationToken ct)
    {lock(gate) {if(!Grants.TryGetValue(hash,out var g) || g.Client!=client || g.SsoGrant.ExpiresAt<=now) return Task.FromResult<SsoGrant?>(null);Grants.Remove(hash);Grants.Add(newHash,g);return Task.FromResult<SsoGrant?>(g.SsoGrant);}}
    public Task RevokeAsync(string hash,string client,DateTimeOffset now,CancellationToken ct) {lock(gate) {if(Grants.TryGetValue(hash,out var g) && g.Client==client)Grants.Remove(hash);}return Task.CompletedTask;}
}

}
