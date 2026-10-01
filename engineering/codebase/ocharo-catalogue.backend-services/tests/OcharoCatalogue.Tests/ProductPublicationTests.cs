using System.Text.Json.Nodes;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Tests;

public sealed class ProductPublicationTests
{
    private static ProductLocale Approved(string name) => new(LocaleStatus.Approved, name, "Adras", null, "A woven layer.");
    private static ProductPricing Prices(string status = PricingStatus.Confirmed) => new("UZS", status, [new(1, 9, 130_000), new(10, null, 100_000)], null);
    private static ProductDocument Ready() => new()
    {
        Id = "nimcha-v01", GarmentType = "nimcha", VariantCode = "v01", Status = ProductStatus.Active,
        Locales = new(Approved("Nimcha"), ProductLocale.Empty(), new(LocaleStatus.Draft, "Unreviewed vest", null, null, null))
        {
            Additional = new Dictionary<string, ProductLocale> { ["fr"] = Approved("Gilet"), ["ar"] = new(LocaleStatus.Draft, "Unreviewed Arabic", null, null, null) },
        },
        Pricing = Prices(),
        Media = [new("front", MediaRole.Cutout, "front.png", "image/png", 200, new string('a', 64), 60, 90, false, "Front", null)],
    };
    private static ProductInput Input(ProductDocument product, int revision) => new()
    {
        ExpectedRevision = revision, GarmentType = product.GarmentType, VariantCode = product.VariantCode,
        Status = product.Status, Locales = product.Locales, Pricing = product.Pricing, Issues = product.Issues,
        Media = product.Media.Select(image => new MediaInput(image.Id, image.Role, image.Alt, image.IsActive, image.Group)).ToList(),
    };
    private static async Task Seed(CatalogueHarness harness, ProductDocument product) =>
        await harness.Get<ICatalogueRepository>().InsertProductAsync(ProductService.ToRecord(product, 1, harness.Clock.UtcNow, harness.Clock.UtcNow), "Test fixture", default);

    [Fact]
    public void Legacy_json_stays_readable_and_additional_languages_round_trip_without_fallback()
    {
        var node = JsonNode.Parse(CatalogueJson.Write(Ready()))!;
        node["locales"]!.AsObject().Remove("additional");
        node["media"]![0]!.AsObject().Remove("isActive");
        node["media"]![0]!.AsObject().Remove("group");
        var old = CatalogueJson.Read<ProductDocument>(node.ToJsonString());
        Assert.True(old.Media[0].IsActive);
        Assert.Empty(old.Locales.Additional);
        Assert.Null(old.Locales.For("fr").Name);
        Assert.Equal("Nimcha", old.Locales.For("uz").Name);
        Assert.Equal("Gilet", CatalogueJson.Read<ProductDocument>(CatalogueJson.Write(Ready())).Locales.For("fr").Name);
        Assert.Equal(25, CatalogueLocales.All.Count);
        Assert.Equal("rtl", CatalogueLocales.Direction("ar"));
        Assert.Equal("ltr", CatalogueLocales.Direction("fr"));
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("missing-language")]
    [InlineData("unapproved-language")]
    [InlineData("empty-name")]
    [InlineData("no-active-image")]
    [InlineData("open-review")]
    public void Eligibility_rejects_each_unconfirmed_boundary(string condition)
    {
        var product = Ready(); var locale = "fr";
        product = condition switch
        {
            "draft" => product with { Status = ProductStatus.Draft },
            "empty-name" => product with { Locales = product.Locales with { Additional = new Dictionary<string, ProductLocale> { ["fr"] = Approved(" ") } } },
            "no-active-image" => product with { Media = [product.Media[0] with { IsActive = false }] },
            "open-review" => product with { Issues = [new("review", "fabric", IssueStatus.Open, "Confirm fabric")] },
            _ => product,
        };
        if (condition == "missing-language") locale = "de";
        if (condition == "unapproved-language") locale = "en";
        Assert.False(ProductPublicationEligibility.Eligible(product, locale));
        Assert.Throws<CatalogueException>(() => PublicCatalogueService.ToPublic(product, 1, DateTimeOffset.UtcNow, locale, new Dictionary<string, GarmentTypeDefinition>()));
    }

    [Fact]
    public async Task Public_feed_uses_exact_approved_locale_and_active_media_and_hides_other_drafts()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var ready = Ready() with { Media = [Ready().Media[0] with { IsActive = false }, Ready().Media[0] with { Id = "model", Sha256 = new string('b', 64), Role = MediaRole.Model, Group = "detail" }] };
        await Seed(harness, ready);
        var service = harness.Get<PublicCatalogueService>();
        var published = Assert.Single((await service.BuildAsync("fr", default)).Products);
        Assert.Equal("Gilet", published.Name);
        Assert.Equal("detail", Assert.Single(published.Images).Group);
        Assert.Equal(new string('b', 64), published.Images[0].Sha256);
        Assert.Null(published.Locales.En.Name);
        Assert.False(published.Locales.Additional.ContainsKey("ar"));
        Assert.Equal("Gilet", published.Locales.For("fr").Name);
        Assert.Equal(2, published.Pricing!.Tiers.Count);
        Assert.Empty((await service.BuildAsync("de", default)).Products);
        Assert.Empty((await service.BuildAsync("en", default)).Products);
        await Assert.ThrowsAsync<CatalogueException>(() => service.BuildAsync("invalid", default));
    }

    [Fact]
    public void Only_confirmed_valid_uzs_prices_are_public()
    {
        foreach (var pricing in new[] { Prices(PricingStatus.Draft), Prices() with { Currency = "USD" }, Prices() with { Tiers = [new(1, null, 130_000), new(10, null, 100_000)] } })
        {
            var product = PublicCatalogueService.ToPublic(Ready() with { Pricing = pricing }, 1, DateTimeOffset.UtcNow, "fr", new Dictionary<string, GarmentTypeDefinition>());
            Assert.Null(product.Pricing);
        }
    }

    [Fact]
    public async Task Active_product_cannot_remove_or_disable_its_final_image_but_draft_can()
    {
        await using var harness = await CatalogueHarness.CreateAsync(); await Seed(harness, Ready());
        var service = harness.Get<ProductService>();
        var input = Input(Ready(), 1);
        var removed = await Assert.ThrowsAsync<CatalogueException>(() => service.UpdateAsync("nimcha-v01", input with { Media = [] }, default));
        var disabled = await Assert.ThrowsAsync<CatalogueException>(() => service.UpdateAsync("nimcha-v01", input with { Media = [new("front", MediaRole.Cutout, null, false)] }, default));
        Assert.Contains("active image", removed.Message); Assert.Contains("active image", disabled.Message);
        Assert.Equal(1, (await service.GetAsync("nimcha-v01", default)).Revision);
        var draft = await service.UpdateAsync("nimcha-v01", input with { Status = ProductStatus.Draft, Media = [] }, default);
        Assert.Empty(draft.Document.Media);
        Assert.Empty((await service.CreateAsync(new ProductInput { GarmentType = "baska" }, default)).Document.Media);
    }

    [Fact]
    public async Task Inactive_images_keep_order_and_legacy_media_updates_preserve_activation_and_group()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var ready = Ready() with { Media = [Ready().Media[0] with { IsActive = false, Group = "fabric" }, Ready().Media[0] with { Id = "model", Sha256 = new string('b', 64), Role = MediaRole.Model }] };
        await Seed(harness, ready);
        var result = await harness.Get<ProductService>().UpdateAsync(ready.Id, Input(ready, 1) with
        { Media = [new("front", MediaRole.Cutout, "Legacy update"), new("model", MediaRole.Model, null)] }, default);
        Assert.False(result.Document.Media[0].IsActive); Assert.Equal("fabric", result.Document.Media[0].Group);
        Assert.Equal("model", ProductService.PrimaryImage(result.Document)!.Id);
    }

    [Fact]
    public async Task Additional_copy_is_validated_duplicated_as_draft_and_preserved_by_legacy_editors()
    {
        await using var harness = await CatalogueHarness.CreateAsync(); await Seed(harness, Ready()); var service = harness.Get<ProductService>();
        var legacy = new ProductLocales(Approved("Nimcha updated"), ProductLocale.Empty(), ProductLocale.Empty());
        var saved = await service.UpdateCopyAsync("nimcha-v01", new(1, legacy), default);
        Assert.Equal("Gilet", saved.Locales.For("fr").Name);
        var copy = await service.DuplicateAsync("nimcha-v01", default);
        Assert.Equal(LocaleStatus.Draft, copy.Document.Locales.For("fr").Status);
        Assert.Equal("Gilet", copy.Document.Locales.For("fr").Name);
        foreach (var invalid in new[]
        {
            legacy with { Additional = new Dictionary<string, ProductLocale> { ["fr"] = Approved("") } },
            legacy with { Additional = new Dictionary<string, ProductLocale> { ["xx"] = Approved("Unknown") } },
            legacy with { Additional = new Dictionary<string, ProductLocale> { ["uz"] = Approved("Duplicate") } },
        }) await Assert.ThrowsAsync<CatalogueException>(() => service.UpdateCopyAsync("nimcha-v01", new(2, invalid), default));
    }

    [Theory]
    [InlineData(1, 10, null)]
    [InlineData(9, 11, null)]
    [InlineData(9, 10, 20)]
    public async Task New_price_sheets_require_complete_one_to_nine_and_ten_plus_bands(int retailMaximum, int wholesaleMinimum, int? wholesaleMaximum)
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var input = new ProductInput { GarmentType = "nimcha", Pricing = Prices() with { Tiers = [new(1, retailMaximum, 100), new(wholesaleMinimum, wholesaleMaximum, 50)] } };
        var error = await Assert.ThrowsAsync<CatalogueException>(() => harness.Get<ProductService>().CreateAsync(input, default));
        Assert.Contains("two price bands", error.Message);
    }

    [Fact]
    public async Task Legacy_bands_remain_readable_and_unchanged_edits_preserve_them()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var ready = Ready() with { Pricing = Prices() with { Tiers = [new(1, 1, 130_000), new(10, null, 100_000)] } }; await Seed(harness, ready);
        var service = harness.Get<ProductService>();
        var updated = await service.UpdateAsync(ready.Id, Input(ready, 1) with { Notes = "Text-only edit" }, default);
        Assert.Equal(1, updated.Document.Pricing!.Tiers[0].MaximumQuantity);
        Assert.Equal(130_000, Assert.Single((await harness.Get<PublicCatalogueService>().BuildAsync("fr", default)).Products).Pricing!.Retail);
        await Assert.ThrowsAsync<CatalogueException>(() => service.UpdateAsync(ready.Id, Input(updated.Document, 2) with
        { Pricing = ready.Pricing with { Tiers = [new(1, 1, 150_000), new(10, null, 100_000)] } }, default));
        var bulk = await service.AdjustPricesAsync(new([ready.Id], "percent", 10), default);
        Assert.Equal(0, bulk.Changed); Assert.Single(bulk.Skipped);
    }

    [Fact]
    public async Task Product_grid_counts_core_and_authored_languages_and_includes_collection_membership()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var ready = Ready() with { Locales = Ready().Locales with { Additional = new Dictionary<string, ProductLocale> { ["fr"] = Approved("Gilet"), ["de"] = ProductLocale.Empty() } } }; await Seed(harness, ready);
        await harness.Get<CollectionService>().SaveAsync(null, new(null, "spring", new("Bahor", null, null), null, [ready.Id], false, 0), default);
        var summary = Assert.Single(await harness.Get<ProductService>().ListAsync(new("Gilet", null, null, null, null), default));
        Assert.Equal(4, summary.LanguagesTotal); Assert.Equal(2, summary.LocalesApproved); Assert.Equal(2, summary.UnconfirmedTranslations);
        Assert.Equal(["spring"], summary.CollectionIds);
    }
}
