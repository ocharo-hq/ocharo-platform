using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OcharoCatalogue.Tests.E2E;

/// <summary>Full HTTP pipeline (middleware, sign-in, controllers, seeding) on an in-memory host, signed in as an owner.</summary>
public sealed class CatalogueApiTests
{
    private static WebApplicationFactory<Program> Host(string root, string? source) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseCatalogueSignIn();
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CatalogueTestDatabase.ConnectionKey] = CatalogueTestDatabase.For(Path.Combine(root, "api.db")), [CatalogueTestDatabase.OwnerKey] = CatalogueTestDatabase.OwnerInstance,
                ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
                ["Catalogue:SourceRoot"] = source,
            }));
        });

    [Fact]
    public async Task Catalogue_ShouldSeedOnFirstBootAndCheckRevisions_WhenEditedThroughHttp()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-api", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        CatalogueHarness.WriteWorkspaceProduct(source, "abaya-v01", "abaya", 150_000, 100_000, 120);
        await using var factory = Host(root, source);
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);

        var list = (await client.GetFromJsonAsync<JsonElement>("/api/catalogue/products")).GetProperty("data");
        var detail = (await client.GetFromJsonAsync<JsonElement>("/api/catalogue/products/abaya-v01")).GetProperty("data");
        var document = detail.GetProperty("document");
        var preview = await client.GetAsync(detail.GetProperty("media")[0].GetProperty("previewUrl").GetString());
        var edit = JsonSerializer.Deserialize<Dictionary<string, object?>>(document.GetRawText())!;
        edit["expectedRevision"] = 1;
        edit["tags"] = new[] { "kuz" };
        var saved = await client.PutAsJsonAsync("/api/catalogue/products/abaya-v01", edit);
        var stale = await client.PutAsJsonAsync("/api/catalogue/products/abaya-v01", edit);
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue?locale=en");
        var missing = await client.GetAsync("/api/catalogue/nothing-here");

        list.GetArrayLength().Should().Be(1);
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Content.Headers.ContentType?.MediaType.Should().Be("image/webp");
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        stale.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        feed.GetProperty("products").GetArrayLength().Should().Be(0);
        var approvedCopy = JsonNode.Parse(document.GetProperty("locales").GetRawText())!;
        approvedCopy["en"]!["status"] = "approved";
        var reviewedIssues = JsonNode.Parse(document.GetProperty("issues").GetRawText())!.AsArray();
        foreach (var issue in reviewedIssues) issue!["status"] = "resolved";
        edit["expectedRevision"] = 2;
        edit["locales"] = approvedCopy;
        edit["issues"] = reviewedIssues;
        var approved = await client.PutAsJsonAsync("/api/catalogue/products/abaya-v01", edit);
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        var published = await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue?locale=en");
        published.GetProperty("products")[0].GetProperty("name").GetString().Should().Be("Vest");
        published.GetProperty("products")[0].GetProperty("localeStatus").GetString().Should().Be("approved");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Writes_ShouldBeRejected_WhenOriginIsCrossOrigin()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-api", Guid.NewGuid().ToString("N"));
        await using var factory = Host(root, null);
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/catalogue/products")
        {
            Content = JsonContent.Create(new { garmentType = "nimcha" }),
        };
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_ShouldStoreImage_WhenSentAsMultipartForm()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-api", Guid.NewGuid().ToString("N"));
        await using var factory = Host(root, null);
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        var created = await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "korset" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("document").GetProperty("id").GetString();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(CatalogueHarness.Png(200));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "front.png");
        form.Add(new StringContent("cutout"), "role");
        form.Add(new StringContent("1"), "expectedRevision");

        var uploaded = await client.PostAsync($"/api/catalogue/products/{id}/media", form);
        var body = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("revision").GetInt32().Should().Be(2);
        body.GetProperty("document").GetProperty("media")[0].GetProperty("role").GetString().Should().Be("cutout");
    }

    /// <summary>Opt-in: imports the real workspace records (set OCHARO_WORKSPACE_PRODUCTS) without writing to them.</summary>
    [Fact]
    public async Task Import_ShouldMapRealWorkspaceCatalogue_WhenAvailable()
    {
        var source = Environment.GetEnvironmentVariable("OCHARO_WORKSPACE_PRODUCTS");
        if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source)) return;
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-api", Guid.NewGuid().ToString("N"));
        await using var factory = Host(root, source);
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);

        var products = (await client.GetFromJsonAsync<JsonElement>("/api/catalogue/products")).GetProperty("data");
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue");

        products.GetArrayLength().Should().BeGreaterThanOrEqualTo(30, "imported {0}", products.GetArrayLength());
        feed.GetProperty("products").EnumerateArray().Should().AllSatisfy(product => product.GetProperty("localeStatus").GetString().Should().Be("approved"));
        Console.WriteLine($"REAL IMPORT: {products.GetArrayLength()} products, {feed.GetProperty("products").GetArrayLength()} approved and eligible");
    }
}
