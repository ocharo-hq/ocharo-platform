using System.Net;
using AwesomeAssertions;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Testing;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace BrandWorkspace.Tests.E2E;

public sealed class ApiContractTests
{
    [Theory]
    [InlineData("GET", "/api")]
    [InlineData("GET", "/api/unknown")]
    [InlineData("GET", "/api/unknown.json")]
    [InlineData("POST", "/api/unknown")]
    [InlineData("DELETE", "/api/unknown.json")]
    public async Task UnknownApi_ShouldReturn404Json_WhenNoEndpointMatches(string method, string path)
    {
        await using var factory = CreateFactory("Host=127.0.0.1;Port=1;Database=unavailable;Username=unused;Timeout=1");
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Health_ShouldSeparateLivenessAndReadiness_WhenDatabaseIsUnavailable()
    {
        await using var factory = CreateFactory("Host=127.0.0.1;Port=1;Database=unavailable;Username=unused;Timeout=1");
        using var client = factory.CreateClient();
        (await client.GetAsync("/health/live")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync("/api/system/status")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        factory.Services.GetService<IBackgroundJobClient>().Should().BeNull();
        factory.Services.GetServices<IHostedService>().Should().NotContain(service => service.GetType().Namespace!.StartsWith("Hangfire"));
    }

    [Fact]
    public async Task Host_ShouldReturn400_WhenHostIsOutsideTheConfiguredAllowlist()
    {
        await using var factory = CreateFactory("Host=127.0.0.1;Port=1;Database=unavailable;Username=unused;Timeout=1");
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Host = "unconfigured.invalid";
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
