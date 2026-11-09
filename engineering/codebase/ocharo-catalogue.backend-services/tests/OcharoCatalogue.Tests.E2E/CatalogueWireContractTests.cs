using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OcharoCatalogue.Tests.E2E;

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
                [CatalogueTestDatabase.ConnectionKey] = CatalogueTestDatabase.For(Path.Combine(root, "api.db")), [CatalogueTestDatabase.OwnerKey] = CatalogueTestDatabase.OwnerInstance,
                ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
                ["Catalogue:SeedOnEmpty"] = "false",
            }));
        });

    private static string Root() => Path.Combine(Path.GetTempPath(), "ocharo-catalogue-wire", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StaleSave_ShouldAnswerConflictProblem_WhenRevisionIsOld()
    {
        await using var factory = Host(Root());
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        var created = await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" });
        var document = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("document");
        var edit = JsonSerializer.Deserialize<Dictionary<string, object?>>(document.GetRawText())!;
        edit["expectedRevision"] = 1;
        (await client.PutAsJsonAsync($"/api/catalogue/products/{document.GetProperty("id").GetString()}", edit)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var stale = await client.PutAsJsonAsync($"/api/catalogue/products/{document.GetProperty("id").GetString()}", edit);
        var problem = (await stale.Content.ReadFromJsonAsync<JsonElement>());

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        stale.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        problem.GetProperty("type").GetString().Should().Be("urn:wow-two:error:Conflict");
        problem.GetProperty("code").GetString().Should().Be("Conflict");
        problem.GetProperty("detail").GetString().Should().Be("The product was saved elsewhere (now revision 2). Reload before saving.");
        string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()).Should().BeFalse();
    }

    [Theory]
    [InlineData(new byte[0], 413)]
    [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 415)]
    public async Task Upload_ShouldKeepPayloadStatuses_WhenRefused(byte[] bytes, int expected)
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

        ((int)response.StatusCode).Should().Be(expected);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Stock_ShouldKeepSizeLabelsAsDictionaryKeys_WhenSerialized()
    {
        await using var factory = Host(Root());
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        var created = await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha", sizes = new[] { "S", "XL" } });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("document").GetProperty("id").GetString();

        using var saved = await client.PutAsJsonAsync($"/api/catalogue/products/{id}/stock", new { expectedRevision = 1, stock = new Dictionary<string, int> { ["XL"] = 2 }, madeToOrder = false });
        var stock = (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("stock");

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        stock.GetProperty("XL").GetInt32().Should().Be(2);
        stock.GetProperty("S").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task PublicFeed_ShouldKeepUnenvelopedShapeWithNullFields_WhenRead()
    {
        await using var factory = Host(Root());
        using var client = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        await client.PutAsJsonAsync("/api/catalogue/settings", new
        {
            settings = new { brandName = "Ocharo", currency = "UZS", garmentTypes = new[] { new { key = "nimcha", uz = "Nimcha", ru = "Жилет", en = "Vest" } }, sizePreset = Array.Empty<string>(), contactLines = Array.Empty<string>(), priceNote = (string?)null },
            expectedRevision = 0,
        });

        var feed = await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue?locale=en");

        feed.TryGetProperty("data", out _).Should().BeFalse();
        feed.GetProperty("priceNote").ValueKind.Should().Be(JsonValueKind.Null);
        feed.GetProperty("products").ValueKind.Should().Be(JsonValueKind.Array);
    }
}
