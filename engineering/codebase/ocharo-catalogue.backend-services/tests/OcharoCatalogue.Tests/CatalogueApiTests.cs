using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OcharoCatalogue.Tests;

/// <summary>Full HTTP pipeline (middleware, controllers, seeding) on an in-memory host.</summary>
public sealed class CatalogueApiTests
{
    private static WebApplicationFactory<Program> Host(string root, string? source) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OcharoCatalogue"] = $"Data Source={Path.Combine(root, "api.db")};Pooling=False",
                ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
                ["Catalogue:SourceRoot"] = source,
            }));
        });

    [Fact]
    public async Task Seeds_on_first_boot_then_edits_through_http_with_revision_checks()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-api", Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "source");
        CatalogueHarness.WriteWorkspaceProduct(source, "abaya-v01", "abaya", 150_000, 100_000, 120);
        await using var factory = Host(root, source);
        var client = factory.CreateClient();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/catalogue/products");
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/catalogue/products/abaya-v01");
        var document = detail.GetProperty("document");
        var preview = await client.GetAsync(detail.GetProperty("media")[0].GetProperty("previewUrl").GetString());
        var edit = JsonSerializer.Deserialize<Dictionary<string, object?>>(document.GetRawText())!;
        edit["expectedRevision"] = 1;
        edit["tags"] = new[] { "kuz" };
        var saved = await client.PutAsJsonAsync("/api/catalogue/products/abaya-v01", edit);
        var stale = await client.PutAsJsonAsync("/api/catalogue/products/abaya-v01", edit);
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue?locale=en");
        var missing = await client.GetAsync("/api/catalogue/nothing-here");

        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("image/webp", preview.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("application/problem+json", stale.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, feed.GetProperty("products").GetArrayLength());
        var approvedCopy = JsonNode.Parse(document.GetProperty("locales").GetRawText())!;
        approvedCopy["en"]!["status"] = "approved";
        var reviewedIssues = JsonNode.Parse(document.GetProperty("issues").GetRawText())!.AsArray();
        foreach (var issue in reviewedIssues) issue!["status"] = "resolved";
        edit["expectedRevision"] = 2;
        edit["locales"] = approvedCopy;
        edit["issues"] = reviewedIssues;
        var approved = await client.PutAsJsonAsync("/api/catalogue/products/abaya-v01", edit);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var published = await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue?locale=en");
        Assert.Equal("Vest", published.GetProperty("products")[0].GetProperty("name").GetString());
        Assert.Equal("approved", published.GetProperty("products")[0].GetProperty("localeStatus").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Rejects_cross_origin_writes()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-api", Guid.NewGuid().ToString("N"));
        await using var factory = Host(root, null);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/catalogue/products")
        {
            Content = JsonContent.Create(new { garmentType = "nimcha" }),
        };
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Uploads_an_image_as_multipart_form()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-api", Guid.NewGuid().ToString("N"));
        await using var factory = Host(root, null);
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "korset" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("document").GetProperty("id").GetString();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(CatalogueHarness.Png(200));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "front.png");
        form.Add(new StringContent("cutout"), "role");
        form.Add(new StringContent("1"), "expectedRevision");

        var uploaded = await client.PostAsync($"/api/catalogue/products/{id}/media", form);
        var body = await uploaded.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        Assert.Equal(2, body.GetProperty("revision").GetInt32());
        Assert.Equal("cutout", body.GetProperty("document").GetProperty("media")[0].GetProperty("role").GetString());
    }

    /// <summary>Opt-in: imports the real workspace records (set OCHARO_WORKSPACE_PRODUCTS) without writing to them.</summary>
    [Fact]
    public async Task Imports_the_real_workspace_catalogue_when_available()
    {
        var source = Environment.GetEnvironmentVariable("OCHARO_WORKSPACE_PRODUCTS");
        if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source)) return;
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-api", Guid.NewGuid().ToString("N"));
        await using var factory = Host(root, source);
        var client = factory.CreateClient();

        var products = await client.GetFromJsonAsync<JsonElement>("/api/catalogue/products");
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue");

        Assert.True(products.GetArrayLength() >= 30, $"imported {products.GetArrayLength()}");
        Assert.All(feed.GetProperty("products").EnumerateArray(), product => Assert.Equal("approved", product.GetProperty("localeStatus").GetString()));
        Console.WriteLine($"REAL IMPORT: {products.GetArrayLength()} products, {feed.GetProperty("products").GetArrayLength()} approved and eligible");
    }
}
