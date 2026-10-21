using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OcharoBrand.Api.RuntimeConfig.Settings;

namespace OcharoBrand.Tests;

/// <summary>Brand serves the Ocharo app addresses as public runtime settings, anonymously and uncached.</summary>
public sealed class RuntimeConfigTests
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
        await using var host = BrandHost.Create(EveryApp);
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync("/api/runtime-config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(new Dictionary<string, string>
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
        }, await ReadAsync(response));
    }

    [Fact]
    public async Task RuntimeConfig_ShouldAnswerWithoutASession_WhenSsoGuardsTheRestOfTheApi()
    {
        await using var host = BrandHost.Create(EveryApp, sso: true);
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(BrandHost.SsoOrigin), AllowAutoRedirect = false, HandleCookies = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/runtime-config");
        request.Headers.Add("Cookie", "__Host-ocharo-brand=forged");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains("OCHARO_DESK_URL", (await ReadAsync(response)).Keys);
        // The SSO boundary still holds for the private API.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/brand/draft")).StatusCode);
    }

    [Fact]
    public async Task RuntimeConfig_ShouldStayBehindTheLoopbackBoundary_WhenSsoIsOff()
    {
        await using var host = BrandHost.Create(EveryApp);
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://brand.example") });

        using var response = await client.GetAsync("/api/runtime-config");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RuntimeConfig_ShouldDropEveryAddressThatIsNotAPlainHttpsLink()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>
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
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync("/api/runtime-config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new Dictionary<string, string> { ["OCHARO_ATLAS_URL"] = "https://ocharo-atlas-dev.example.ts.net/" }, await ReadAsync(response));
    }

    [Fact]
    public async Task RuntimeConfig_ShouldServeAnEmptyObject_WhenNoAddressIsConfigured()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        using var response = await client.GetAsync("/api/runtime-config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("{}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Startup_ShouldWarnOnceNamingEveryRejectedAddress()
    {
        var logs = new CapturedLogs();
        await using var host = BrandHost.Create(new Dictionary<string, string?>
        {
            ["OcharoApps:Desk"] = "http://ocharo-desk-dev.example.ts.net/",
            ["OcharoApps:Brand"] = "https://ocharo-brand-dev.example.ts.net/?token=abc",
            ["OcharoApps:Atlas"] = "https://ocharo-atlas-dev.example.ts.net/",
        }, logs: logs);
        using var client = host.Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/runtime-config")).StatusCode);

        var warning = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.StartsWith("Runtime config"));
        Assert.Equal("Runtime config leaves out OcharoApps:Desk, OcharoApps:Brand: each must be a plain HTTPS address.", warning.Message);
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
        Assert.Equal(accepted, OcharoAppsSettingsExtensions.IsPublicAddress(value));
    }

    [Fact]
    public void RejectedSettings_ShouldNameConfiguredAddressesItLeavesOut()
    {
        var settings = new OcharoAppsSettings { Desk = "http://desk.example.com/", Atlas = "https://atlas.example.com/", Wear = " " };

        Assert.Equal(["OcharoApps:Desk"], settings.RejectedSettings());
    }

    private static async Task<Dictionary<string, string>> ReadAsync(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(await response.Content.ReadAsStringAsync())!;
    }
}

/// <summary>An isolated Brand host with its own SQLite file and blob folder, optionally behind enabled SSO.</summary>
/// <remarks>The seed folder does not exist, so an empty store seeds without logos and no real artwork is read.</remarks>
internal sealed class BrandHost : IAsyncDisposable
{
    public const string SsoOrigin = "https://brand.example";

    private readonly string root;

    private BrandHost(string root, WebApplicationFactory<Program> factory)
    {
        this.root = root;
        Factory = factory;
    }

    public WebApplicationFactory<Program> Factory { get; }

    public static BrandHost Create(IReadOnlyDictionary<string, string?> settings, bool sso = false, CapturedLogs? logs = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-brand-host", Guid.NewGuid().ToString("N"));
        var configuration = new Dictionary<string, string?>(settings)
        {
            ["ConnectionStrings:OcharoBrand"] = $"Data Source={Path.Combine(root, "test.db")};Pooling=False",
            ["Brand:DataRoot"] = Path.Combine(root, "data"),
            ["Brand:SeedLogoRoot"] = Path.Combine(root, "missing-logos"),
        };
        if (sso)
        {
            configuration["Identity:Sso:Enabled"] = "true";
            configuration["Identity:Sso:DeskOrigin"] = "https://desk.example";
            configuration["Identity:Sso:ClientOrigin"] = SsoOrigin;
            configuration["Identity:Sso:ClientId"] = "brand";
            configuration["Identity:Sso:ClientSecret"] = new string('s', 48);
            configuration["Identity:Sso:OrganizationId"] = Guid.NewGuid().ToString();
        }
        Directory.CreateDirectory(root);
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            if (logs is not null)
                builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs));
        });
        return new BrandHost(root, factory);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        try { Directory.Delete(root, true); } catch (IOException) { }
    }
}

/// <summary>Records every host log entry, so a test can count the warnings startup writes.</summary>
internal sealed class CapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(Entries);

    public void Dispose() { }

    private sealed class Logger(ConcurrentQueue<(LogLevel Level, string Message)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
