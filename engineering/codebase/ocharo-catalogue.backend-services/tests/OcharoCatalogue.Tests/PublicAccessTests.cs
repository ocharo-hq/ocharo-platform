using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OcharoCatalogue.Tests;

/// <summary>
/// The owner opened the deployed pilots to the public on 2026-10-24, until the Ocharo identity server lands: a deployed
/// host accepts remote callers without sign-in, a local run keeps the loopback boundary, and <c>Catalogue:PublicAccess</c>
/// overrides either way. Cross-origin writes stay refused.
/// </summary>
public sealed class PublicAccessTests
{
    private const string Origin = "https://catalogue.example";

    [Theory]
    [InlineData(null, null, HttpStatusCode.Forbidden)]
    [InlineData("dev", null, HttpStatusCode.OK)]
    [InlineData("prod", null, HttpStatusCode.OK)]
    [InlineData("prod", "false", HttpStatusCode.Forbidden)]
    [InlineData(null, "true", HttpStatusCode.OK)]
    public async Task RemoteCaller_ShouldReachTheEditingApi_OnlyOnAPublicHost(string? deployEnvironment, string? publicAccess, HttpStatusCode expected)
    {
        var settings = new Dictionary<string, string?>();
        if (deployEnvironment is not null) settings["DEPLOY_ENVIRONMENT"] = deployEnvironment;
        if (publicAccess is not null) settings["Catalogue:PublicAccess"] = publicAccess;
        await using var host = CatalogueHost.Create(settings);
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(Origin), AllowAutoRedirect = false });

        using var response = await client.GetAsync("/api/catalogue/products");

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example", true)]
    [InlineData("http://catalogue.example", true)]
    [InlineData(Origin, false)]
    public async Task PublicHost_ShouldStillRefuseCrossOriginWrites(string origin, bool refused)
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?> { ["DEPLOY_ENVIRONMENT"] = "prod" });
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(Origin), AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/catalogue/products") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Origin", origin);

        using var response = await client.SendAsync(request);

        Assert.Equal(refused, response.StatusCode == HttpStatusCode.Forbidden);
    }
}
