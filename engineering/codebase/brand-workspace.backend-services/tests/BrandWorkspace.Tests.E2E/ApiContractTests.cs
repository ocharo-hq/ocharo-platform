using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;
using Xunit;

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
    public async Task Health_ShouldSeparateLivenessAndReadiness_WhenDatabaseIsUnavailable()
    {
        // Startup applies the schema, so the database leaves only after the API is up.
        var container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
        await using var unavailable = new PostgresFixture(container);
        await unavailable.StartAsync();
        await using var factory = CreateFactory(unavailable.ConnectionString);
        using var client = factory.CreateClient();
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
        await container.StopAsync();
        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync("/api/system/status")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        factory.Services.GetService<IBackgroundJobClient>().Should().BeNull();
        factory.Services.GetServices<IHostedService>().Should().NotContain(service => service.GetType().Namespace!.StartsWith("Hangfire"));
    }

    [Fact]
    public async Task Host_ShouldReturn400_WhenHostIsOutsideTheConfiguredAllowlist()
    {
        await using var factory = CreateFactory(database.Connection);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Host = "unconfigured.invalid";
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetInt32().Should().Be(400);
    }

    internal static WebApiTestHost<BrandWorkspace.Api.ApiAssemblyMarker> CreateFactory(string connection)
    {
        var factory = new WebApiTestHost<BrandWorkspace.Api.ApiAssemblyMarker>
        {
            ConfigureHostHook = builder => builder.UseEnvironment(Environments.Development),
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

/// <summary>One PostgreSQL database for contract tests; the first API host to start applies its schema.</summary>
public sealed class ApiDatabaseFixture : IAsyncLifetime
{
    private readonly PostgresFixture _database = new(new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build());
    public string Connection => _database.ConnectionString;
    public async Task InitializeAsync() => await _database.StartAsync();
    public async Task DisposeAsync() => await _database.DisposeAsync();
}
