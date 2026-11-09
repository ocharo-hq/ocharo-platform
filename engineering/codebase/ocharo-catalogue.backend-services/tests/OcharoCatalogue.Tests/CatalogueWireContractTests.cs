using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OcharoCatalogue.Tests;

/// <summary>The HTTP wire after the move to the mediator: enveloped private payloads, SDK problem details with the
/// catalogue's statuses, data-keyed dictionaries kept as stored, and the public feed's frozen shape.</summary>
public sealed class CatalogueWireContractTests
{
    private static WebApplicationFactory<Program> Host(string root) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseCatalogueSignIn();
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OcharoCatalogue"] = $"Data Source={Path.Combine(root, "api.db")};Pooling=False",
                ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
                ["Catalogue:SeedOnEmpty"] = "false",
            }));
        });

    private static string Root() => Path.Combine(Path.GetTempPath(), "ocharo-catalogue-wire", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Stale_save_answers_a_conflict_problem_from_the_shared_factory()
    {
        await using var factory = Host(Root());
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        var created = await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" });
        var document = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("document");
        var edit = JsonSerializer.Deserialize<Dictionary<string, object?>>(document.GetRawText())!;
        edit["expectedRevision"] = 1;
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/catalogue/products/{document.GetProperty("id").GetString()}", edit)).StatusCode);

        using var stale = await client.PutAsJsonAsync($"/api/catalogue/products/{document.GetProperty("id").GetString()}", edit);
        var problem = (await stale.Content.ReadFromJsonAsync<JsonElement>());

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("application/problem+json", stale.Content.Headers.ContentType?.MediaType);
        Assert.Equal("urn:wow-two:error:Conflict", problem.GetProperty("type").GetString());
        Assert.Equal("Conflict", problem.GetProperty("code").GetString());
        Assert.Equal("The product was saved elsewhere (now revision 2). Reload before saving.", problem.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData(new byte[0], 413)]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 415)]
    public async Task Refused_uploads_keep_their_payload_statuses(byte[] bytes, int expected)
    {
        await using var factory = Host(Root());
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        var created = await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "korset" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("document").GetProperty("id").GetString();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "front.png");
        form.Add(new StringContent("cutout"), "role");

        using var response = await client.PostAsync($"/api/catalogue/products/{id}/media", form);

        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Stock_keeps_size_labels_as_dictionary_keys()
    {
        await using var factory = Host(Root());
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        var created = await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha", sizes = new[] { "S", "XL" } });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("document").GetProperty("id").GetString();

        using var saved = await client.PutAsJsonAsync($"/api/catalogue/products/{id}/stock", new { expectedRevision = 1, stock = new Dictionary<string, int> { ["XL"] = 2 }, madeToOrder = false });
        var stock = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("stock");

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(2, stock.GetProperty("XL").GetInt32());
        Assert.Equal(0, stock.GetProperty("S").GetInt32());
    }

    [Fact]
    public async Task Public_feed_keeps_its_unenveloped_shape_with_null_fields()
    {
        await using var factory = Host(Root());
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        await client.PutAsJsonAsync("/api/catalogue/settings", new
        {
            settings = new { brandName = "Ocharo", currency = "UZS", garmentTypes = new[] { new { key = "nimcha", uz = "Nimcha", ru = "Жилет", en = "Vest" } }, sizePreset = Array.Empty<string>(), contactLines = Array.Empty<string>(), priceNote = (string?)null },
            expectedRevision = 0,
        });

        var feed = await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue?locale=en");

        Assert.False(feed.TryGetProperty("data", out _));
        Assert.Equal(JsonValueKind.Null, feed.GetProperty("priceNote").ValueKind);
        Assert.Equal(JsonValueKind.Array, feed.GetProperty("products").ValueKind);
    }
}
