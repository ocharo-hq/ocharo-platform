using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using BrandWorkspace.Domain.Commerce;
using OcharoBrand.Domain.Brand;
using OcharoCatalogue.Domain.Catalogue.Models;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises durable translation/group revisions and compatibility with three-language clients.</summary>
public sealed class CommerceProductRevisionTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    [Fact]
    public async Task TranslationsAndGroups_IncrementProductRevision_AndOmittedLegacyFieldsSurvive()
    {
        var owner = await fixture.CreateOwnerAsync(); using var client = await fixture.LoginAsync(owner);
        using var brandResponse = await client.PostAsJsonAsync($"/api/organizations/{owner.OrganizationId}/brands", new { name = "Product fixture", timeZone = "Asia/Tashkent" });
        var brandId = (await Data(brandResponse)).GetProperty("id").GetGuid();
        var path = $"/api/organizations/{owner.OrganizationId}/brands/{brandId}/commerce";
        var product = new CommerceProduct(new ProductDocument { Id = "vest", GarmentType = "vest", VariantCode = "v01", Status = "draft", Locales = ProductLocales.Empty() });
        var snapshot = new CommerceSnapshot { Slug = "products-" + Guid.NewGuid().ToString("N"), Identity = new BrandIdentity { Name = "Product fixture" }, Products = [product] };
        using var saved = await client.PutAsJsonAsync(path, new { expectedRevision = 0, snapshot });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var draft = await Data(saved); draft.GetProperty("snapshot").GetProperty("products")[0].GetProperty("revision").GetInt32().Should().Be(1);
        var translated = snapshot with { Products = [product with { Translations = new Dictionary<string, ProductLocale> { ["fr"] = new("approved", "Gilet", "Soie", null, null) } }] };
        using var updated = await client.PutAsJsonAsync(path, new { expectedRevision = 1, snapshot = translated });
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        draft = await Data(updated); draft.GetProperty("snapshot").GetProperty("products")[0].GetProperty("revision").GetInt32().Should().Be(2);
        var grouped = translated with { Products = [translated.Products[0] with { MediaGroups = [new("model-photos", "Model photos", "model", [])] }] };
        using var groups = await client.PutAsJsonAsync(path, new { expectedRevision = 2, snapshot = grouped });
        groups.StatusCode.Should().Be(HttpStatusCode.OK, await groups.Content.ReadAsStringAsync());
        draft = await Data(groups); draft.GetProperty("snapshot").GetProperty("products")[0].GetProperty("revision").GetInt32().Should().Be(3);
        var legacy = JsonNode.Parse(draft.GetProperty("snapshot").GetRawText())!;
        var oldProduct = legacy["products"]![0]!.AsObject(); oldProduct.Remove("translations"); oldProduct.Remove("mediaGroups");
        using var oldClient = await client.PutAsJsonAsync(path, new { expectedRevision = 3, snapshot = legacy });
        oldClient.StatusCode.Should().Be(HttpStatusCode.OK, await oldClient.Content.ReadAsStringAsync());
        draft = await Data(oldClient); var retained = draft.GetProperty("snapshot").GetProperty("products")[0];
        retained.GetProperty("revision").GetInt32().Should().Be(3);
        retained.GetProperty("translations").GetProperty("fr").GetProperty("name").GetString().Should().Be("Gilet");
        retained.GetProperty("mediaGroups")[0].GetProperty("id").GetString().Should().Be("model-photos");
        await fixture.SetRoleAsync(owner, "editor");
        var editorSnapshot = JsonNode.Parse(draft.GetProperty("snapshot").GetRawText())!;
        editorSnapshot["products"]![0]!["translations"]!["fr"]!["name"] = "New French title";
        using var edited = await client.PutAsJsonAsync(path, new { expectedRevision = 4, snapshot = editorSnapshot });
        edited.StatusCode.Should().Be(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
        (await Data(edited)).GetProperty("snapshot").GetProperty("products")[0].GetProperty("translations").GetProperty("fr").GetProperty("status").GetString().Should().Be("draft");
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response) => JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync()).GetProperty("data");
}
