using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;
using Xunit;

namespace OcharoTrends.Tests.E2E;

public sealed class TrendRouteTests
{
    private static readonly Guid Organization = Guid.Parse("df2dedc9-984f-4a2f-87d2-b63d231dae67");
    private const string ReviewUrl = "/api/trends/review/videos";
    private const string VideoUrl = ReviewUrl + "/video-1";
    private const string PublicUrl = "/api/trends/public/guides";
    private const string PublicGuide = """
        {"slug":"shoe-sweep","title":"Shoe sweep","kind":"trend","summary":"Move across the shoes.",
         "steps":["Start at shoe height."],"examples":[{"title":"Creator example","url":"https://example.test/watch"}]}
        """;
    private const string Fixture = """
        {"videos":[{"id":"video-1","title":"A useful technique","creator":"Test creator","format":"short",
         "url":"https://example.test/watch"}],"guides":[
        """ + PublicGuide + "]}";

    [Fact]
    public async Task PublicRoutesOnlyReadTheExplicitRelease()
    {
        using var directory = new Fixtures();
        await using var preview = CreateHost(directory.SeedSettings);
        using var previewClient = Client(preview);
        Assert.Empty((await previewClient.GetFromJsonAsync<JsonElement>(PublicUrl)).EnumerateArray());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await previewClient.GetAsync(ReviewUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await previewClient.PutAsJsonAsync(VideoUrl, Useful())).StatusCode);

        await using var release = CreateHost(directory.ReleaseSettings);
        using var publicClient = Client(release);
        var guide = Assert.Single((await publicClient.GetFromJsonAsync<JsonElement>(PublicUrl)).EnumerateArray());
        Assert.Equal("shoe-sweep", guide.GetProperty("slug").GetString());
        Assert.False(guide.TryGetProperty("note", out _));
    }

    [Fact]
    public async Task ReviewPersistsAcrossHostsAndRequiresMembershipPermissionAndCsrf()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build();
        await database.StartAsync();
        using var directory = new Fixtures();
        var settings = new Dictionary<string, string?>(directory.SeedSettings)
        {
            ["ConnectionStrings:Database"] = database.GetConnectionString(),
            ["Identity:Client:Authority"] = "https://identity.example.test/",
            ["Identity:Client:ClientId"] = "ocharo-trends",
            ["Identity:Client:ClientSecret"] = "test-secret",
            ["Trends:OrganizationId"] = Organization.ToString("D"),
            ["Migrations:OwnerInstance"] = "ocharo-trends@test",
            ["LOCAL_SETTINGS"] = "true",
        };
        using var bootSettings = new BootSettings(settings);

        await using (var host = CreateHost(settings, testIdentity: true))
        {
            using var anonymous = Client(host);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(ReviewUrl)).StatusCode);
            Assert.Empty((await anonymous.GetFromJsonAsync<JsonElement>(PublicUrl)).EnumerateArray());

            using var outsider = Client(host, "outsider");
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(ReviewUrl)).StatusCode);

            using var viewer = Client(host, "viewer");
            await AddCsrfAsync(viewer);
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(ReviewUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync(VideoUrl, Useful())).StatusCode);

            using var editor = Client(host, "editor");
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsJsonAsync(VideoUrl, Useful())).StatusCode);
            await AddCsrfAsync(editor);
            Assert.Equal(HttpStatusCode.BadRequest, (await editor.PutAsJsonAsync(VideoUrl, new { status = "bogus", note = "" })).StatusCode);
            var response = await editor.PutAsJsonAsync(VideoUrl, Useful());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("useful", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        }

        await using var restarted = CreateHost(settings, testIdentity: true);
        using var member = Client(restarted, "viewer");
        var video = Assert.Single((await member.GetFromJsonAsync<JsonElement>(ReviewUrl)).EnumerateArray());
        Assert.Equal("Useful at 0:02", video.GetProperty("note").GetString());
    }

    private static object Useful() => new { status = "useful", note = "Useful at 0:02" };

    private static WebApplicationFactory<Program> CreateHost(Dictionary<string, string?> settings, bool testIdentity = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(WebHostDefaults.ApplicationKey, typeof(Program).Assembly.FullName);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
            if (testIdentity)
                builder.ConfigureServices(services => services.PostConfigure<AuthenticationOptions>(options =>
                {
                    var cookie = options.Schemes.Single(scheme => scheme.Name == BffAuthenticationSchemeConstants.Cookie);
                    cookie.HandlerType = typeof(TestIdentity);
                }));
        });

    private static HttpClient Client(WebApplicationFactory<Program> host, string? role = null)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });
        if (role is not null)
            client.DefaultRequestHeaders.Add("X-Trends-Test-Role", role);
        return client;
    }

    private static async Task AddCsrfAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/identity/session");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Add("X-Csrf-Token", session.GetProperty("data").GetProperty("csrfToken").GetString());
    }

    private sealed class Fixtures : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("ocharo-trends-");

        internal Fixtures()
        {
            var seedFile = Path.Combine(directory.FullName, "seed.json");
            var releaseFile = Path.Combine(directory.FullName, "release.json");
            File.WriteAllText(seedFile, Fixture);
            File.WriteAllText(releaseFile, "[" + PublicGuide + "]");
            SeedSettings = new Dictionary<string, string?> { ["Trends:SeedPath"] = seedFile };
            ReleaseSettings = new Dictionary<string, string?>(SeedSettings) { ["Trends:PublishedGuidePath"] = releaseFile };
        }

        internal Dictionary<string, string?> SeedSettings { get; }
        internal Dictionary<string, string?> ReleaseSettings { get; }

        public void Dispose() => directory.Delete(recursive: true);
    }

    // The service chooses public preview or full review before WebApplicationFactory applies its host configuration.
    // Tests in this class run serially. Restore the process boot settings before another test starts.
    private sealed class BootSettings : IDisposable
    {
        private readonly Dictionary<string, string?> original = new(StringComparer.Ordinal);

        internal BootSettings(Dictionary<string, string?> settings)
        {
            foreach (var (key, value) in settings)
            {
                var variable = key.Replace(":", "__", StringComparison.Ordinal);
                original.Add(variable, Environment.GetEnvironmentVariable(variable));
                Environment.SetEnvironmentVariable(variable, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in original)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private sealed class TestIdentity(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Trends-Test-Role"].ToString();
            if (role is not ("viewer" or "editor" or "outsider"))
                return Task.FromResult(AuthenticateResult.NoResult());

            var organization = role == "outsider" ? Guid.Empty.ToString("D") : Organization.ToString("D");
            var session = $"test-session-{role}";
            var claims = new List<Claim>
            {
                new("sub", role), new("sid", session), new("name", role),
                new(OrganizationClaimTypeConstants.Id, organization),
                new(OrganizationClaimTypeConstants.Role, $"{organization}:{role}"),
            };
            if (role == "editor")
                claims.Add(new Claim(OrganizationClaimTypeConstants.Permission, $"{organization}:trends:edit"));

            var properties = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) };
            properties.Items[".bff.sid"] = session;
            properties.Items[".bff.ref"] = "test-reference";
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, properties, Scheme.Name)));
        }
    }
}
