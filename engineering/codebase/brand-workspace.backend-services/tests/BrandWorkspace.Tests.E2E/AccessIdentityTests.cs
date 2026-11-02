using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Desk reads the person and organization from the Identity token's claims; the test host stands in for the BFF cookie.</summary>
public sealed class AccessIdentityTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    [Fact]
    public async Task Session_ShouldReportSignedOut_WhenAnonymous()
    {
        using var client = DeskTestHost.CreateClient(fixture.Factory);

        using var response = await client.GetAsync("/api/identity/session");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await Data(response)).GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData("/api/organizations")]
    [InlineData("/api/organizations/00000000-0000-0000-0000-000000000001/events")]
    public async Task OrganizationRoutes_ShouldReturn401_WhenAnonymous(string path)
    {
        using var client = DeskTestHost.CreateClient(fixture.Factory);

        using var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Organizations_ShouldListOnlyTheTokensOrganizationWithItsRole()
    {
        var owner = await fixture.CreateOwnerAsync();
        await fixture.CreateOwnerAsync();
        using var client = await fixture.LoginAsync(owner);

        var listed = await Data(await client.GetAsync("/api/organizations"));
        listed.GetArrayLength().Should().Be(1);
        listed[0].GetProperty("id").GetGuid().Should().Be(owner.OrganizationId);
        listed[0].GetProperty("role").GetString().Should().Be("owner");

        await fixture.SetRoleAsync(owner, "editor");
        (await Data(await client.GetAsync("/api/organizations")))[0].GetProperty("role").GetString().Should().Be("editor");

        await fixture.SetRoleAsync(owner, null);
        (await Data(await client.GetAsync("/api/organizations"))).GetArrayLength().Should().Be(0, "a token without an organization names none");
    }

    [Fact]
    public async Task OrganizationRoute_ShouldReturn404_WhenItIsNotTheTokensOrganization()
    {
        var owner = await fixture.CreateOwnerAsync();
        var other = await fixture.CreateOwnerAsync();
        using var client = await fixture.LoginAsync(owner);

        using var own = await client.GetAsync($"/api/organizations/{owner.OrganizationId}/events");
        using var foreign = await client.GetAsync($"/api/organizations/{other.OrganizationId}/events");
        await fixture.SetRoleAsync(owner, null);
        using var withoutMembership = await client.GetAsync($"/api/organizations/{owner.OrganizationId}/events");

        own.StatusCode.Should().Be(HttpStatusCode.OK, await own.Content.ReadAsStringAsync());
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        withoutMembership.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FirstOrganizationRequest_ShouldProjectTheNewPersonIntoDesk()
    {
        var owner = await fixture.CreateOwnerAsync();
        var subject = Guid.NewGuid().ToString("D");
        var email = $"projected-{Guid.NewGuid():N}@example.test";
        DeskTestHost.Register(new(subject, owner.OrganizationId, "editor", "Projected editor", email));
        (await Projection(subject)).Should().BeNull("Desk has not met this person yet");
        using var client = DeskTestHost.CreateClient(fixture.Factory, subject);

        using var response = await client.GetAsync($"/api/organizations/{owner.OrganizationId}/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await Projection(subject)).Should().Be(("Projected editor", email));
    }

    [Fact]
    public async Task OwnerOnlyAction_ShouldBeRefusedToAnEditor_WhoseTokenLacksTheApprovePermission()
    {
        var owner = await fixture.CreateOwnerAsync();
        await fixture.SetRoleAsync(owner, "editor");
        using var client = await fixture.LoginAsync(owner);

        using var response = await client.PostAsJsonAsync(
            $"/api/organizations/{owner.OrganizationId}/brands/{Guid.NewGuid()}/commerce/adopt-preserved", new { slug = "synthetic-adoption" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Owner approval is required.");
    }

    [Fact]
    public async Task IntegrationTickets_ShouldAnswer503_UntilIdentityIssuesAgentTokens()
    {
        var owner = await fixture.CreateOwnerAsync();
        using var client = await fixture.LoginAsync(owner);

        using var response = await client.PostAsJsonAsync(
            $"/api/organizations/{owner.OrganizationId}/integration-tickets", new { audience = "ocharo-desk-mcp" });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).Should().Contain("integration_tickets_unavailable");
    }

    private async Task<(string Name, string? Email)?> Projection(string subject)
    {
        await using var command = fixture.DataSource.CreateCommand("""SELECT "DisplayName","Email" FROM access.users WHERE id=@id""");
        command.Parameters.AddWithValue("id", subject);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1)) : null;
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }
}
