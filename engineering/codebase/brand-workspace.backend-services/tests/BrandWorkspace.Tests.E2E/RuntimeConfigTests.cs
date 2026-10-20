using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api;
using BrandWorkspace.Api.RuntimeConfig.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Beta.Be.Testing;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Desk serves the Ocharo app addresses as public runtime settings, anonymously and uncached.</summary>
public sealed class RuntimeConfigTests(ApiDatabaseFixture database) : IClassFixture<ApiDatabaseFixture>
{
    private static readonly Dictionary<string, string?> EveryApp = new()
    {
        ["OcharoApps:Desk"] = "https://ocharo-desk-dev.example.ts.net/",
        ["OcharoApps:Catalogue"] = "https://ocharo-catalogue-dev.example.ts.net/catalogues",
        ["OcharoApps:Studio"] = "https://ocharo-studio-dev.example.ts.net/studio",
        ["OcharoApps:Marketing"] = "https://ocharo-marketing-dev.example.ts.net/",
        ["OcharoApps:Editorial"] = "https://ocharo-blog-dev.example.ts.net/studio/",
        ["OcharoApps:Brand"] = "https://ocharo-brand-dev.example.ts.net/",
        ["OcharoApps:Atlas"] = "https://ocharo-atlas-dev.example.ts.net/",
        ["OcharoApps:Wear"] = "https://ocharo-wear-dev.example.ts.net/",
        ["OcharoApps:Blog"] = "https://ocharo-blog-dev.example.ts.net/",
    };

    [Fact]
    public async Task RuntimeConfig_ShouldServeEveryConfiguredAddressAnonymouslyAndUncached()
    {
        await using var factory = CreateFactory(EveryApp);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

        using var response = await client.GetAsync("/api/runtime-config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await ReadAsync(response)).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["OCHARO_DESK_URL"] = "https://ocharo-desk-dev.example.ts.net/",
            ["OCHARO_CATALOGUE_URL"] = "https://ocharo-catalogue-dev.example.ts.net/catalogues",
            ["OCHARO_STUDIO_URL"] = "https://ocharo-studio-dev.example.ts.net/studio",
            ["OCHARO_MARKETING_URL"] = "https://ocharo-marketing-dev.example.ts.net/",
            ["OCHARO_EDITORIAL_URL"] = "https://ocharo-blog-dev.example.ts.net/studio/",
            ["OCHARO_BRAND_URL"] = "https://ocharo-brand-dev.example.ts.net/",
            ["OCHARO_ATLAS_URL"] = "https://ocharo-atlas-dev.example.ts.net/",
            ["OCHARO_WEAR_URL"] = "https://ocharo-wear-dev.example.ts.net/",
            ["OCHARO_BLOG_URL"] = "https://ocharo-blog-dev.example.ts.net/",
        });
        // The default-deny boundary still holds for the rest of the API.
        (await client.GetAsync("/api/organizations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("https://localhost")]
    [InlineData("http://localhost")]
    public async Task RuntimeConfig_ShouldAnswerWithoutASession_WhenTheCookieIsForged(string origin)
    {
        await using var factory = CreateFactory(EveryApp);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(origin), HandleCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/runtime-config");
        request.Headers.Add("Cookie", "__Host-brand-workspace-session=forged");

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await ReadAsync(response)).Should().ContainKey("OCHARO_CATALOGUE_URL");
    }

    [Fact]
    public async Task RuntimeConfig_ShouldDropEveryAddressThatIsNotAPlainHttpsLink()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["OcharoApps:Desk"] = "http://ocharo-desk-dev.example.ts.net/",
            ["OcharoApps:Catalogue"] = "https://operator:secret@ocharo-catalogue-dev.example.ts.net/",
            ["OcharoApps:Studio"] = "https://ocharo-studio-dev.example.ts.net/studio?token=abc",
            ["OcharoApps:Marketing"] = "https://ocharo-marketing-dev.example.ts.net/#access_token=abc",
            ["OcharoApps:Editorial"] = "/studio/",
            ["OcharoApps:Brand"] = "not a link",
            ["OcharoApps:Atlas"] = "https://ocharo-atlas-dev.example.ts.net/",
            ["OcharoApps:Wear"] = "",
            ["OcharoApps:Blog"] = "javascript:alert(1)",
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync("/api/runtime-config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAsync(response)).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["OCHARO_ATLAS_URL"] = "https://ocharo-atlas-dev.example.ts.net/",
        });
    }

    [Fact]
    public async Task RuntimeConfig_ShouldServeAnEmptyObject_WhenNoAddressIsConfigured()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync("/api/runtime-config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadAsStringAsync()).Should().Be("{}");
    }

    [Theory]
    [InlineData("https://ocharo-desk-dev.example.ts.net/", true)]
    [InlineData("https://desk.example.com:8443/organizations", true)]
    [InlineData("https://example.com/@handle", true)]
    [InlineData("http://desk.example.com/", false)]
    [InlineData("https://user@desk.example.com/", false)]
    [InlineData("https://desk.example.com/?", false)]
    [InlineData("https://desk.example.com/#", false)]
    [InlineData("https://desk.example.com/a b", false)]
    [InlineData(" https://desk.example.com/", false)]
    [InlineData("//desk.example.com/", false)]
    [InlineData("/organizations", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPublicAddress_ShouldAcceptOnlyPlainAbsoluteHttpsLinks(string? value, bool accepted)
    {
        OcharoAppsSettingsExtensions.IsPublicAddress(value).Should().Be(accepted);
    }

    [Fact]
    public void RejectedSettings_ShouldNameConfiguredAddressesItLeavesOut()
    {
        var settings = new OcharoAppsSettings { Desk = "http://desk.example.com/", Atlas = "https://atlas.example.com/", Wear = " " };

        settings.RejectedSettings().Should().Equal("OcharoApps:Desk");
    }

    private WebApiTestHost<ApiAssemblyMarker> CreateFactory(IReadOnlyDictionary<string, string?> apps)
    {
        var factory = new WebApiTestHost<ApiAssemblyMarker>
        {
            ConfigureHostHook = builder => builder.UseEnvironment(Environments.Development),
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>(apps)
                {
                    ["ConnectionStrings:Database"] = database.Connection,
                    ["AllowedHosts"] = "localhost;127.0.0.1"
                })
        };
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        return factory;
    }

    private static async Task<Dictionary<string, string>> ReadAsync(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(await response.Content.ReadAsStringAsync())!;
    }
}
