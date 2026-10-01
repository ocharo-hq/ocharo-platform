using System.Security.Claims;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api.Integrations;
using BrandWorkspace.Application.Access;
using Microsoft.Extensions.Configuration;
using WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Checks the real SDK-issued ticket against current Platform session and membership authority.</summary>
public sealed class IntegrationTicketTests
{
    private const string Key = "local-test-only-integration-signing-key-2026";
    private readonly Clock clock = new();
    private readonly Access access = new();
    private IntegrationTickets Service => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Integrations:SigningKey"] = Key }).Build(), clock, access);
    private string Issue() => JsonSerializer.SerializeToElement(Service.Issue("ocharo-studio", access.Member!, access.Session)).GetProperty("token").GetString()!;
    private async Task<JsonElement> Inspect(string token, string audience = "ocharo-studio") => JsonSerializer.SerializeToElement(await Service.IntrospectAsync(token,audience,default));

    [Fact]
    public async Task Ticket_RechecksMembershipAndRole()
    {
        var token = Issue();
        (await Inspect(token)).GetProperty("canApprove").GetBoolean().Should().BeTrue();
        access.Member = access.Member! with { Role="viewer",CanApprove=false,CanEdit=false };
        var viewer = await Inspect(token);
        viewer.GetProperty("active").GetBoolean().Should().BeTrue();
        viewer.GetProperty("canEdit").GetBoolean().Should().BeFalse();
        access.Member = null;
        (await Inspect(token)).GetProperty("active").GetBoolean().Should().BeFalse();
    }
    [Fact]
    public async Task Ticket_RejectsRevokedSessionWrongAudienceAndExpiredLifetime()
    {
        var token = Issue();
        (await Inspect(token,"ocharo-marketing")).GetProperty("active").GetBoolean().Should().BeFalse();
        access.Active = false;
        (await Inspect(token)).GetProperty("active").GetBoolean().Should().BeFalse();
        access.Active = true;
        clock.Now += TimeSpan.FromMinutes(5);
        (await Inspect(token)).GetProperty("active").GetBoolean().Should().BeFalse();
    }
    [Theory]
    [InlineData("sub")]
    [InlineData("org")]
    [InlineData("sid")]
    public async Task Ticket_RejectsMissingRequiredClaimWithoutThrowing(string missing)
    {
        var claims = new[] { new Claim("sub","operator"),new Claim("org",access.Organization.ToString()),new Claim("sid",access.Session.ToString()) };
        var token = new JwtTokenIssuer(new JwtTokenIssuerOptions { Issuer="ocharo-platform/integration-v1",Audience="ocharo-studio",SigningKey=Key,Lifetime=TimeSpan.FromMinutes(5) },clock).Issue(claims.Where(c=>c.Type!=missing));
        (await Inspect(token)).GetProperty("active").GetBoolean().Should().BeFalse();
    }
    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public async Task Ticket_RejectsMalformedToken(string token) => (await Inspect(token)).GetProperty("active").GetBoolean().Should().BeFalse();
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now {get;set;} = new(2026,9,29,0,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Access : IAccessRepository
    {
        public Guid Organization {get;} = Guid.NewGuid();
        public Guid Session {get;} = Guid.NewGuid();
        public bool Active {get;set;} = true;
        public OrganizationAccess? Member {get;set;}
        public Access() => Member = new(Organization,"operator","owner",true,true);
        public Task<bool> IsSessionActiveAsync(Guid sessionId,string userId,DateTimeOffset now,CancellationToken token) => Task.FromResult(Active && sessionId==Session && userId=="operator");
        public Task<OrganizationAccess?> FindMembershipAsync(Guid organizationId,string userId,CancellationToken token) => Task.FromResult(organizationId==Organization && userId=="operator" ? Member : null);
        public Task CreateSessionAsync(Guid sessionId,string userId,string securityStamp,DateTimeOffset expiresAt,Guid? replacedSessionId,CancellationToken token) => throw new NotSupportedException();
        public Task RevokeSessionAsync(Guid sessionId,DateTimeOffset now,CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> ListOrganizationsAsync(string userId,CancellationToken token) => throw new NotSupportedException();
        public Task<Organization?> CreateOrganizationAsync(string name,string slug,string userId,CancellationToken token) => throw new NotSupportedException();
    }
}
