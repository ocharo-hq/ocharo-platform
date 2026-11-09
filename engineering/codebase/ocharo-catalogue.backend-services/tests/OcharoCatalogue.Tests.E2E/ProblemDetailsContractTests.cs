using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OcharoCatalogue.Tests.E2E;

/// <summary>Real HTTP error contracts over isolated temporary stores.</summary>
public sealed class ProblemDetailsContractTests
{
    [Theory]
    [InlineData("GET", "/api/missing-contract-route", null, null, 404)]
    [InlineData("PATCH", "/api/catalogue/settings", null, null, 404)]
    [InlineData("POST", "/api/catalogue/designs", "application/json", "{", 400)]
    [InlineData("POST", "/api/catalogue/designs", "text/plain", "invalid", 415)]
    public async Task Errors_ShouldRemainProblemJson_WhenHtmlIsRequested(string method, string path, string? contentType, string? body, int expected)
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-problem-contract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseCatalogueSignIn();
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [CatalogueTestDatabase.ConnectionKey] = CatalogueTestDatabase.For(Path.Combine(root, "api.db")), [CatalogueTestDatabase.OwnerKey] = CatalogueTestDatabase.OwnerInstance,
                    ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
                    ["Catalogue:SourceRoot"] = Path.Combine(root, "no-source"),
                }));
            });
            using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            request.Headers.Accept.ParseAdd("text/html");
            if (body is not null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, contentType!);
            using var response = await client.SendAsync(request);
            ((int)response.StatusCode).Should().Be(expected);
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
            response.Headers.CacheControl?.NoStore.Should().BeTrue();
            var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            problem.GetProperty("status").GetInt32().Should().Be(expected);
            string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()).Should().BeFalse();
            string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()).Should().BeFalse();
            problem.GetProperty("instance").GetString().Should().Be(path);
            string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()).Should().BeFalse();
            string.IsNullOrWhiteSpace(problem.GetProperty("requestId").GetString()).Should().BeFalse();
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
