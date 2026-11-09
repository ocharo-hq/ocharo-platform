using System.Text.Json.Nodes;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoCatalogue.Tests.Integration;

public sealed class ProductPublicationTests
{
    private static ProductInput Input(ProductDocument product, int revision) => new()
    {
        ExpectedRevision = revision, GarmentType = product.GarmentType, VariantCode = product.VariantCode,
        Status = product.Status, Locales = product.Locales, Pricing = product.Pricing, Issues = product.Issues,
        Media = product.Media.Select(image => new MediaInput(image.Id, image.Role, image.Alt, image.IsActive, image.Group)).ToList(),
    };
    private static async Task Seed(CatalogueHarness harness, ProductDocument product) =>
        await harness.Get<ICatalogueRepository>().InsertProductAsync(ProductProjections.ToRecord(product, 1, harness.Clock.GetUtcNow(), harness.Clock.GetUtcNow()), "Test fixture", default);

    [Fact]
    public async Task PublicFeed_ShouldUseExactApprovedLocaleAndActiveMediaAndHideOtherDrafts_WhenBuilt()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var ready = ProductFixtures.Ready() with { Media = [ProductFixtures.Ready().Media[0] with { IsActive = false }, ProductFixtures.Ready().Media[0] with { Id = "model", Sha256 = new string('b', 64), Role = MediaRole.Model, Group = "detail" }] };
        await Seed(harness, ready);
        var service = harness.Feed;
        var published = (await service.BuildAsync("fr", default)).Products.Should().ContainSingle().Subject;
        published.Name.Should().Be("Gilet");
        published.Images.Should().ContainSingle().Subject.Group.Should().Be("detail");
        published.Images[0].Sha256.Should().Be(new string('b', 64));
        published.Locales.En.Name.Should().BeNull();
        published.Locales.Additional.ContainsKey("ar").Should().BeFalse();
        published.Locales.For("fr").Name.Should().Be("Gilet");
        published.Pricing!.Tiers.Count.Should().Be(2);
        (await service.BuildAsync("de", default)).Products.Should().BeEmpty();
        (await service.BuildAsync("en", default)).Products.Should().BeEmpty();
        await FluentActions.Awaiting(() => service.BuildAsync("invalid", default)).Should().ThrowAsync<CatalogueRejection>();
    }

    [Fact]
    public async Task FinalImage_ShouldStayOnActiveProductButGoOnDraft_WhenRemovedOrDisabled()
    {
        await using var harness = await CatalogueHarness.CreateAsync(); await Seed(harness, ProductFixtures.Ready());
        var service = harness.Products;
        var input = Input(ProductFixtures.Ready(), 1);
        var removed = (await FluentActions.Awaiting(() => service.UpdateAsync("nimcha-v01", input with { Media = [] }, default)).Should().ThrowAsync<CatalogueRejection>()).Which;
        var disabled = (await FluentActions.Awaiting(() => service.UpdateAsync("nimcha-v01", input with { Media = [new("front", MediaRole.Cutout, null, false)] }, default)).Should().ThrowAsync<CatalogueRejection>()).Which;
        removed.Message.Should().Contain("active image"); disabled.Message.Should().Contain("active image");
        (await service.GetAsync("nimcha-v01", default)).Revision.Should().Be(1);
        var draft = await service.UpdateAsync("nimcha-v01", input with { Status = ProductStatus.Draft, Media = [] }, default);
        draft.Document.Media.Should().BeEmpty();
        (await service.CreateAsync(new ProductInput { GarmentType = "baska" }, default)).Document.Media.Should().BeEmpty();
    }

    [Fact]
    public async Task Media_ShouldKeepOrderAndPreserveActivationAndGroup_WhenInactiveOrLegacyUpdated()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var ready = ProductFixtures.Ready() with { Media = [ProductFixtures.Ready().Media[0] with { IsActive = false, Group = "fabric" }, ProductFixtures.Ready().Media[0] with { Id = "model", Sha256 = new string('b', 64), Role = MediaRole.Model }] };
        await Seed(harness, ready);
        var result = await harness.Products.UpdateAsync(ready.Id, Input(ready, 1) with
        { Media = [new("front", MediaRole.Cutout, "Legacy update"), new("model", MediaRole.Model, null)] }, default);
        result.Document.Media[0].IsActive.Should().BeFalse(); result.Document.Media[0].Group.Should().Be("fabric");
        ProductProjections.PrimaryImage(result.Document)!.Id.Should().Be("model");
    }

    [Fact]
    public async Task AdditionalCopy_ShouldBeValidatedDuplicatedAsDraftAndPreserved_WhenEdited()
    {
        await using var harness = await CatalogueHarness.CreateAsync(); await Seed(harness, ProductFixtures.Ready()); var service = harness.Products;
        var legacy = new ProductLocales(ProductFixtures.Approved("Nimcha updated"), ProductLocale.Empty(), ProductLocale.Empty());
        var saved = await service.UpdateCopyAsync("nimcha-v01", new(1, legacy), default);
        saved.Locales.For("fr").Name.Should().Be("Gilet");
        var copy = await service.DuplicateAsync("nimcha-v01", default);
        copy.Document.Locales.For("fr").Status.Should().Be(LocaleStatus.Draft);
        copy.Document.Locales.For("fr").Name.Should().Be("Gilet");
        foreach (var invalid in new[]
        {
            legacy with { Additional = new Dictionary<string, ProductLocale> { ["fr"] = ProductFixtures.Approved("") } },
            legacy with { Additional = new Dictionary<string, ProductLocale> { ["xx"] = ProductFixtures.Approved("Unknown") } },
            legacy with { Additional = new Dictionary<string, ProductLocale> { ["uz"] = ProductFixtures.Approved("Duplicate") } },
        }) await FluentActions.Awaiting(() => service.UpdateCopyAsync("nimcha-v01", new(2, invalid), default)).Should().ThrowAsync<CatalogueRejection>();
    }

    [Theory]
    [InlineData(1, 10, null)]
    [InlineData(9, 11, null)]
    [InlineData(9, 10, 20)]
    public async Task PriceSheet_ShouldRequireCompleteBands_WhenNewSheetIsSaved(int retailMaximum, int wholesaleMinimum, int? wholesaleMaximum)
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var input = new ProductInput { GarmentType = "nimcha", Pricing = ProductFixtures.Prices() with { Tiers = [new(1, retailMaximum, 100), new(wholesaleMinimum, wholesaleMaximum, 50)] } };
        var error = (await FluentActions.Awaiting(() => harness.Products.CreateAsync(input, default)).Should().ThrowAsync<CatalogueRejection>()).Which;
        error.Message.Should().Contain("two price bands");
    }

    [Fact]
    public async Task LegacyBands_ShouldRemainReadableAndPreserved_WhenEditsLeaveThemUnchanged()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var ready = ProductFixtures.Ready() with { Pricing = ProductFixtures.Prices() with { Tiers = [new(1, 1, 130_000), new(10, null, 100_000)] } }; await Seed(harness, ready);
        var service = harness.Products;
        var updated = await service.UpdateAsync(ready.Id, Input(ready, 1) with { Notes = "Text-only edit" }, default);
        updated.Document.Pricing!.Tiers[0].MaximumQuantity.Should().Be(1);
        (await harness.Feed.BuildAsync("fr", default)).Products.Should().ContainSingle().Subject.Pricing!.Retail.Should().Be(130_000);
        await FluentActions.Awaiting(() => service.UpdateAsync(ready.Id, Input(updated.Document, 2) with
        { Pricing = ready.Pricing with { Tiers = [new(1, 1, 150_000), new(10, null, 100_000)] } }, default)).Should().ThrowAsync<CatalogueRejection>();
        var bulk = await service.AdjustPricesAsync(new([ready.Id], "percent", 10), default);
        bulk.Changed.Should().Be(0); bulk.Skipped.Should().ContainSingle();
    }

    [Fact]
    public async Task ProductGrid_ShouldCountCoreAndAuthoredLanguagesAndIncludeCollectionMembership_WhenListed()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var ready = ProductFixtures.Ready() with { Locales = ProductFixtures.Ready().Locales with { Additional = new Dictionary<string, ProductLocale> { ["fr"] = ProductFixtures.Approved("Gilet"), ["de"] = ProductLocale.Empty() } } }; await Seed(harness, ready);
        await harness.Collections.SaveAsync(null, new(null, "spring", new("Bahor", null, null), null, [ready.Id], false, 0), default);
        var summary = (await harness.Products.ListAsync(new("Gilet", null, null, null, null), default)).Should().ContainSingle().Subject;
        summary.LanguagesTotal.Should().Be(4); summary.LocalesApproved.Should().Be(2); summary.UnconfirmedTranslations.Should().Be(2);
        summary.CollectionIds.Should().BeEquivalentTo(["spring"], options => options.WithStrictOrdering());
    }
}
