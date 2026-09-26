using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using BrandWorkspace.Persistence.Access;
using Xunit;

namespace BrandWorkspace.Tests.E2E.Access;

public sealed class AccessContractTests(AccessDatabaseFixture database) : IClassFixture<AccessDatabaseFixture>
{
    [Fact]
    public async Task Access_ShouldFailClosed_WhenAuthenticationOrAntiforgeryIsMissing()
    {
        await using var factory = database.CreateFactory();
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var operatorUser = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        var anonymous = await AccessDatabaseFixture.SessionAsync(client);
        anonymous.GetProperty("authenticated").GetBoolean().Should().BeFalse();
        anonymous.GetProperty("setupRequired").GetBoolean().Should().BeFalse();
        (await client.GetAsync("/api/organizations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var noCsrf = await AccessDatabaseFixture.PostAsync(client, "/api/auth/login",
            new { email = operatorUser.Email, password = "Test-only-Correct#927" }, null);
        noCsrf.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        noCsrf.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await noCsrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("csrf_validation_failed");
        var token = await AccessDatabaseFixture.LoginAsync(client, operatorUser);
        var invalid = await AccessDatabaseFixture.PostAsync(client, "/api/organizations", new { name = "Sample", slug = "sample" }, "wrong");
        invalid.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var malformed = new HttpRequestMessage(HttpMethod.Post, "/api/organizations")
        { Content = new StringContent("{broken", System.Text.Encoding.UTF8, "application/json") };
        malformed.Headers.Add("X-CSRF-TOKEN", token);
        var malformedResponse = await client.SendAsync(malformed);
        malformedResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        malformedResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        malformedResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var insecure = factory.CreateClient();
        (await insecure.GetAsync("/api/auth/session")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Identity_ShouldLockTheAccount_WhenFivePasswordsFail()
    {
        await using var factory = database.CreateFactory();
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var user = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        var csrf = (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("csrfToken").GetString();
        for (var attempt = 0; attempt < 5; attempt++)
            (await AccessDatabaseFixture.PostAsync(client, "/api/auth/login", new { email = user.Email, password = "wrong" }, csrf))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await AccessDatabaseFixture.PostAsync(client, "/api/auth/login", new { email = user.Email, password = "Test-only-Correct#927" }, csrf))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccessUser>>();
        (await users.IsLockedOutAsync((await users.FindByIdAsync(user.Id))!)).Should().BeTrue();
    }

    [Fact]
    public async Task Session_ShouldRevokeAndRotateCsrf_WhenTheSameOperatorSignsInAgain()
    {
        await using var factory = database.CreateFactory();
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var user = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        var firstToken = await AccessDatabaseFixture.LoginAsync(client, user);
        var firstSession = (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("sessionId").GetGuid();
        var secondToken = await AccessDatabaseFixture.LoginAsync(client, user);
        var secondSession = (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("sessionId").GetGuid();
        secondSession.Should().NotBe(firstSession);
        (await AccessDatabaseFixture.PostAsync(client, "/api/organizations", new { name = "Stale", slug = "stale" }, firstToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var logout = await AccessDatabaseFixture.PostAsync(client, "/api/auth/logout", new { }, secondToken);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("authenticated").GetBoolean().Should().BeFalse();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<BrandWorkspace.Application.Access.IAccessRepository>();
        (await repository.IsSessionActiveAsync(firstSession, user.Id, DateTimeOffset.UtcNow, default)).Should().BeFalse();
        (await repository.IsSessionActiveAsync(secondSession, user.Id, DateTimeOffset.UtcNow, default)).Should().BeFalse();
    }

    [Fact]
    public async Task Session_ShouldRevokePreviousOperator_WhenAuthenticatedLoginChangesAccount()
    {
        await using var factory = database.CreateFactory();
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var first = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        var second = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        await AccessDatabaseFixture.LoginAsync(client, first);
        var old = (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("sessionId").GetGuid();
        await AccessDatabaseFixture.LoginAsync(client, second);
        (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("user").GetProperty("id").GetString().Should().Be(second.Id);
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<BrandWorkspace.Application.Access.IAccessRepository>();
        (await repository.IsSessionActiveAsync(old, first.Id, DateTimeOffset.UtcNow, default)).Should().BeFalse();
    }

    [Theory]
    [InlineData("UPDATE access.sessions SET revoked_at=now() WHERE user_id=$1")]
    [InlineData("UPDATE access.sessions SET expires_at=now()-interval '1 second' WHERE user_id=$1")]
    [InlineData("UPDATE access.users SET \"IsActive\"=false WHERE id=$1")]
    [InlineData("UPDATE access.users SET \"SecurityStamp\"='replaced' WHERE id=$1")]
    public async Task Session_ShouldRejectExistingCookie_WhenDurableAuthorityChanges(string sql)
    {
        await using var factory = database.CreateFactory();
        using var client = AccessDatabaseFixture.CreateClient(factory);
        var user = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        await AccessDatabaseFixture.LoginAsync(client, user);
        await database.ExecuteAsync(sql, user.Id);
        (await client.GetAsync("/api/organizations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("authenticated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Membership_ShouldScopeAndRecheckAuthority_WhenMembershipChanges()
    {
        await using var factory = database.CreateFactory();
        using var owner = AccessDatabaseFixture.CreateClient(factory);
        using var other = AccessDatabaseFixture.CreateClient(factory);
        var ownerUser = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        var otherUser = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        var ownerCsrf = await AccessDatabaseFixture.LoginAsync(owner, ownerUser);
        var otherCsrf = await AccessDatabaseFixture.LoginAsync(other, otherUser);
        var slug = "organization-" + Guid.NewGuid().ToString("N");
        var created = await AccessDatabaseFixture.PostAsync(owner, "/api/organizations",
            new { name = "Organization", slug, userId = otherUser.Id }, ownerCsrf);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var organization = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        organization.GetProperty("role").GetString().Should().Be("owner");
        var id = organization.GetProperty("id").GetGuid();
        var path = $"/api/organizations/{id}/brands";
        (await owner.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await other.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var otherList = await other.GetFromJsonAsync<JsonElement>("/api/organizations");
        otherList.GetProperty("data").GetArrayLength().Should().Be(0);
        (await AccessDatabaseFixture.PostAsync(owner, "/api/organizations", new { name = "Duplicate", slug }, ownerCsrf))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        await database.ExecuteAsync("INSERT INTO access.memberships(organization_id,user_id,role) VALUES ($1,$2,'viewer')", id, otherUser.Id);
        (await other.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AccessDatabaseFixture.PostAsync(other, path, new { name = "Forbidden", timeZone = "Asia/Tashkent" }, otherCsrf))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await database.ExecuteAsync("DELETE FROM access.memberships WHERE organization_id=$1 AND user_id=$2", id, otherUser.Id);
        (await other.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cookie_ShouldSurviveHostRestartAndRejectOtherProduct_WhenKeysArePersisted()
    {
        string cookie;
        await using (var factory = database.CreateFactory())
        {
            using var client = AccessDatabaseFixture.CreateClient(factory);
            var user = await AccessDatabaseFixture.CreateOperatorAsync(factory);
            var csrf = (await AccessDatabaseFixture.SessionAsync(client)).GetProperty("csrfToken").GetString();
            var login = await AccessDatabaseFixture.PostAsync(client, "/api/auth/login",
                new { email = user.Email, password = "Test-only-Correct#927" }, csrf);
            login.EnsureSuccessStatusCode();
            var header = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-brand-workspace-session="));
            header.ToLowerInvariant().Should().Contain("secure").And.Contain("httponly").And.Contain("samesite=lax");
            cookie = header.Split(';')[0];
        }
        await using (var restarted = database.CreateFactory())
        {
            using var client = AccessDatabaseFixture.CreateClient(restarted, cookies: false);
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            (await client.GetAsync("/api/organizations")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        await using (var foreign = database.CreateFactory("unrelated-product:test"))
        {
            using var client = AccessDatabaseFixture.CreateClient(foreign, cookies: false);
            client.DefaultRequestHeaders.Add("Cookie", cookie);
            (await client.GetAsync("/api/organizations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
