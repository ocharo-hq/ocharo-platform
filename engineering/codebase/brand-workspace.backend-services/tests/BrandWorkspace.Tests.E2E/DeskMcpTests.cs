using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api;
using BrandWorkspace.Api.Mcp;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Beta.Be.Testing;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Verifies real MCP transport against delegated sessions, organization isolation and stored records.</summary>
public sealed class DeskMcpTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    private const string SigningKey = "desk-mcp-synthetic-signing-key-2026-42";
    private const string Audience = DeskMcpAuthenticationHandler.Audience;

    [Fact]
    public async Task DefaultHost_ShouldKeepMcpDisabled()
    {
        using var client = fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await SendAsync(client, "tools/list");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Endpoint_ShouldRejectAnonymousCookieOnlyAndWrongAudienceClients()
    {
        await using var factory = CreateFactory();
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using var operatorClient = await LoginAsync(factory, owner);
        using var anonymous = BearerClient(factory);
        (await SendAsync(anonymous, "tools/list")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(operatorClient, "tools/list")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var otherAudience = BearerClient(factory, await IssueAsync(operatorClient, owner, "ocharo-studio"));
        (await SendAsync(otherAudience, "tools/list")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        using var malformed = BearerClient(factory, "malformed-ticket");
        using var invalid = await SendAsync(malformed, "tools/list");
        invalid.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        invalid.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        invalid.Headers.WwwAuthenticate.Single().Scheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task Tools_ShouldExposeOnlyBoundedReadOperationsWithoutOrganizationArguments()
    {
        await using var factory = CreateFactory();
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using var operatorClient = await LoginAsync(factory, owner);
        using var client = BearerClient(factory, await IssueAsync(operatorClient, owner));
        using var initialized = await SendAsync(client, "initialize", new
        {
            protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "desk-test", version = "1" }
        });
        initialized.StatusCode.Should().Be(HttpStatusCode.OK);
        initialized.Headers.Contains("Mcp-Session-Id").Should().BeFalse();
        initialized.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var list = await ReadAsync(await SendAsync(client, "tools/list"));
        var tools = list.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToArray();
        tools.Select(tool => tool.GetProperty("name").GetString()).Should().BeEquivalentTo(
            "list_suppliers", "get_supplier", "list_supplier_media", "list_events", "get_event", "get_event_summary");
        foreach (var tool in tools)
        {
            tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean().Should().BeTrue();
            tool.GetProperty("inputSchema").ToString().Should().NotContain("organizationId").And.NotContain("userId");
        }
        using var invalidPage = await CallAsync(client, "list_events", new { limit = 501 });
        invalidPage.RootElement.GetProperty("result").GetProperty("isError").GetBoolean().Should().BeTrue();
        using var unknown = await CallAsync(client, "create_event", new { title = "Never created" });
        unknown.RootElement.TryGetProperty("error", out _).Should().BeTrue();
        (await client.GetAsync($"/api/organizations/{owner.OrganizationId}/events")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reads_ShouldUseTicketOrganizationAndHideForeignRecords()
    {
        await using var factory = CreateFactory();
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        OrganizationAccess stranger = await fixture.CreateOwnerAsync();
        using var operatorClient = await LoginAsync(factory, owner);
        using var strangerClient = await LoginAsync(factory, stranger);
        Guid ownEvent = (await DataAsync(await operatorClient.PostAsJsonAsync(Events(owner), new
        { title = "Visible market", scope = "local", format = "market", applicationDeadline = "2027-01-01" }))).GetProperty("id").GetGuid();
        Guid foreignEvent = (await DataAsync(await strangerClient.PostAsJsonAsync(Events(stranger), new
        { title = "Other organization event", scope = "local", format = "market" }))).GetProperty("id").GetGuid();
        Guid supplier = (await DataAsync(await operatorClient.PostAsJsonAsync(Suppliers(owner), new
        { name = "Visible mill", stage = "lead" }))).GetProperty("id").GetGuid();
        Guid foreignSupplier = (await DataAsync(await strangerClient.PostAsJsonAsync(Suppliers(stranger), new
        { name = "Other organization mill", stage = "lead" }))).GetProperty("id").GetGuid();
        using var client = BearerClient(factory, await IssueAsync(operatorClient, owner));
        using var events = await CallAsync(client, "list_events");
        events.RootElement.GetProperty("result").ToString().Should().Contain("Visible market").And.NotContain("Other organization");
        using var suppliers = await CallAsync(client, "list_suppliers");
        suppliers.RootElement.GetProperty("result").ToString().Should().Contain("Visible mill").And.NotContain("Other organization");
        using var eventDetail = await CallAsync(client, "get_event", new { eventId = ownEvent });
        eventDetail.RootElement.GetProperty("result").ToString().Should().Contain("Visible market");
        using var supplierDetail = await CallAsync(client, "get_supplier", new { supplierId = supplier });
        supplierDetail.RootElement.GetProperty("result").ToString().Should().Contain("Visible mill");
        using var summary = await CallAsync(client, "get_event_summary", new { today = "2026-10-09" });
        summary.RootElement.GetProperty("result").ToString().Should().Contain("Visible market").And.NotContain("Other organization");
        using var media = await CallAsync(client, "list_supplier_media", new { supplierId = supplier });
        media.RootElement.GetProperty("result").TryGetProperty("isError", out var mediaError).Should().BeFalse();
        using var deniedEvent = await CallAsync(client, "get_event", new { eventId = foreignEvent });
        deniedEvent.RootElement.GetProperty("result").GetProperty("isError").GetBoolean().Should().BeTrue();
        using var deniedSupplier = await CallAsync(client, "get_supplier", new { supplierId = foreignSupplier });
        deniedSupplier.RootElement.GetProperty("result").GetProperty("isError").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("membership")]
    [InlineData("session")]
    [InlineData("expiry")]
    public async Task EachRequest_ShouldRejectRevokedOrExpiredAuthority(string change)
    {
        await using var factory = CreateFactory();
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using var operatorClient = await LoginAsync(factory, owner);
        using var client = BearerClient(factory, await IssueAsync(operatorClient, owner));
        (await SendAsync(client, "tools/list")).StatusCode.Should().Be(HttpStatusCode.OK);
        if (change == "membership") await fixture.SetRoleAsync(owner, null);
        else if (change == "session") (await operatorClient.PostAsync("/api/auth/logout", null)).IsSuccessStatusCode.Should().BeTrue();
        else factory.Clock.Advance(TimeSpan.FromMinutes(6));
        (await SendAsync(client, "tools/list")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("https://localhost/mcp", "https://untrusted.example", HttpStatusCode.Forbidden)]
    [InlineData("https://localhost/mcp/", "https://untrusted.example", HttpStatusCode.Forbidden)]
    [InlineData("https://localhost/MCP", "https://untrusted.example", HttpStatusCode.Forbidden)]
    [InlineData("http://localhost/mcp", null, HttpStatusCode.BadRequest)]
    public async Task Boundary_ShouldRejectCrossOriginAndCleartextRequests(string uri, string? origin, HttpStatusCode status)
    {
        await using var factory = CreateFactory();
        using var client = BearerClient(factory);
        using var response = await SendAsync(client, "tools/list", uri: uri, origin: origin);
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task ConnectionMetadata_ShouldReportActualAvailabilityAndRequireMembership()
    {
        await using var factory = CreateFactory();
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using var operatorClient = await LoginAsync(factory, owner);
        string path = $"/api/organizations/{owner.OrganizationId}/integrations/mcp";
        var metadata = await DataAsync(await operatorClient.GetAsync(path));
        metadata.GetProperty("enabled").GetBoolean().Should().BeTrue();
        metadata.GetProperty("audience").GetString().Should().Be(Audience);
        metadata.GetProperty("tokenLifetimeSeconds").GetInt32().Should().Be(300);
        using var anonymous = BearerClient(factory);
        (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await operatorClient.GetAsync($"/api/organizations/{Guid.NewGuid()}/integrations/mcp")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await using var unsigned = CreateFactory(signingKey: "");
        var unsignedOwner = await fixture.CreateOwnerAsync();
        using var unsignedOwnerClient = await LoginAsync(unsigned, unsignedOwner);
        var unavailable = await DataAsync(await unsignedOwnerClient.GetAsync($"/api/organizations/{unsignedOwner.OrganizationId}/integrations/mcp"));
        unavailable.GetProperty("enabled").GetBoolean().Should().BeFalse();
        unavailable.GetProperty("disabledReason").GetString().Should().Be("signing-key-missing");
    }

    private WebApiTestHost<ApiAssemblyMarker> CreateFactory(string signingKey = SigningKey)
    {
        var factory = new WebApiTestHost<ApiAssemblyMarker>
        {
            ConfigureHostHook = builder => builder.UseEnvironment(Environments.Development),
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = fixture.Factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("Database")!,
                ["AllowedHosts"] = "localhost;127.0.0.1", ["Mcp:Enabled"] = "true", ["Integrations:SigningKey"] = signingKey
            })
        };
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        return factory;
    }

    private async Task<HttpClient> LoginAsync(WebApiTestHost<ApiAssemblyMarker> factory, OrganizationAccess access)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<AccessUser>>().FindByIdAsync(access.UserId);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false });
        var session = await DataAsync(await client.GetAsync("/api/auth/session"));
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.GetProperty("csrfToken").GetString());
        (await client.PostAsJsonAsync("/api/auth/login", new { email = user!.Email, password = "Preparation_Fixture_42!" }))
            .IsSuccessStatusCode.Should().BeTrue();
        session = await DataAsync(await client.GetAsync("/api/auth/session"));
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.GetProperty("csrfToken").GetString());
        return client;
    }

    private static async Task<string> IssueAsync(HttpClient client, OrganizationAccess owner, string audience = Audience)
    {
        var issued = await DataAsync(await client.PostAsJsonAsync($"/api/organizations/{owner.OrganizationId}/integration-tickets", new { audience }));
        return issued.GetProperty("token").GetString()!;
    }

    private static HttpClient BearerClient(WebApiTestHost<ApiAssemblyMarker> factory, string? ticket = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });
        if (ticket is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ticket);
        return client;
    }

    private static string Events(OrganizationAccess access) => $"/api/organizations/{access.OrganizationId}/events";
    private static string Suppliers(OrganizationAccess access) => $"/api/organizations/{access.OrganizationId}/sourcing/suppliers";

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonDocument> CallAsync(HttpClient client, string name, object? arguments = null)
    {
        return await ReadAsync(await SendAsync(client, "tools/call", new { name, arguments = arguments ?? new { } }));
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, object? parameters = null,
        string uri = "/mcp", string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        { Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters ?? new { } }) };
        request.Headers.Add("Accept", "application/json, text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        if (origin is not null) request.Headers.Add("Origin", origin);
        return client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadAsStringAsync();
        var json = body.Split('\n').LastOrDefault(line => line.StartsWith("data: ", StringComparison.Ordinal));
        return JsonDocument.Parse(json is null ? body : json[6..]);
    }
}
