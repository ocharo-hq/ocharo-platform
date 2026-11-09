using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BrandWorkspace.Tests.E2E;

public sealed class ApiContractTests(ApiDatabaseFixture database) : IClassFixture<ApiDatabaseFixture>
{
    [Theory]
    [InlineData("GET", "/api")]
    [InlineData("GET", "/api/unknown")]
    [InlineData("GET", "/api/unknown.json")]
    [InlineData("POST", "/api/unknown")]
    [InlineData("DELETE", "/api/unknown.json")]
    public async Task UnknownApi_ShouldReturn404Json_WhenNoEndpointMatches(string method, string path)
    {
        await using var factory = CreateFactory(database.Connection);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("instance").GetString().Should().Be(path);
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("requestId").GetString().Should().NotBeNullOrWhiteSpace();
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Health_ShouldReportUnavailable_WhenDatabaseIsUnavailable()
    {
        // Startup applies the schema, so the database leaves only after the API is up.
        var container = new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build();
        await using var unavailable = new PostgresFixture(container);
        await unavailable.StartAsync();
        await using var factory = CreateFactory(unavailable.ConnectionString);
        using var client = factory.CreateClient();
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        await container.StopAsync();
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync("/api/system/status")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        factory.Services.GetService<IBackgroundJobClient>().Should().BeNull();
        factory.Services
            .GetServices<IHostedService>()
            .Should()
            .NotContain(service => service.GetType().Namespace!.StartsWith("Hangfire"));
    }

    [Fact]
    public async Task Host_ShouldReturn400_WhenHostIsOutsideTheConfiguredAllowlist()
    {
        await using var factory = CreateFactory(database.Connection);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Host = "unconfigured.invalid";
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .GetProperty("status")
            .GetInt32()
            .Should()
            .Be(400);
    }

    [Fact]
    public async Task StatusRoute_ShouldReportTheInformationalVersion_WhenTheVersionRouteIsRetired()
    {
        await using var factory = CreateFactory(database.Connection);
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<JsonElement>("/api/system/status");
        using var retired = await client.GetAsync("/api/system/version");

        status.GetProperty("service").GetString().Should().Be("brand-workspace");
        status.GetProperty("persistence").GetString().Should().Be("PostgreSQL");
        status.GetProperty("version").GetString().Should().Be(BuildIdentity.Version);
        retired.StatusCode.Should().Be(HttpStatusCode.NotFound);
        retired.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task ResourcePolicy_ShouldBeCrossOriginOnlyOnPublicBrandRoutes()
    {
        await using var factory = CreateFactory(database.Connection);
        using var client = factory.CreateClient();

        using var media = await client.GetAsync($"/api/public/v1/brands/no-such-brand/media/{new string('0', 64)}");
        using var edition =
            await client.GetAsync(
                $"/api/public/v1/brands/no-such-brand/catalogue-editions/{Guid.NewGuid()}/catalogue.pdf");
        using var status = await client.GetAsync("/api/system/status");

        media.StatusCode.Should().Be(HttpStatusCode.NotFound);
        media.Headers.GetValues("Cross-Origin-Resource-Policy").Should().Equal("cross-origin");
        edition.Headers.GetValues("Cross-Origin-Resource-Policy").Should().Equal("cross-origin");
        status.Headers.GetValues("Cross-Origin-Resource-Policy").Should().Equal("same-origin");
    }

    internal static WebApiTestHost<BrandWorkspace.Api.ApiAssemblyMarker> CreateFactory(string connection)
    {
        var factory = new WebApiTestHost<BrandWorkspace.Api.ApiAssemblyMarker>
        {
            ConfigureHostHook = builder => builder.UseEnvironment(Environments.Development).UseIdentitySettings(),
            ConfigureServicesHook = DeskTestHost.Use,
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Database"] = connection,
                    ["AllowedHosts"] = "localhost;127.0.0.1"
                })
        };
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        return factory;
    }
}
