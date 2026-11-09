using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator;

namespace OcharoCatalogue.Tests.Integration;

public sealed class ProductServiceTests
{
    private static ProductInput Draft(string garment = "nimcha") => new()
    {
        GarmentType = garment,
        Locales = new ProductLocales(new ProductLocale(LocaleStatus.Draft, "Nimcha", "Atlas", null, null), ProductLocale.Empty(), ProductLocale.Empty()),
        Sizes = ["S", "M", "M", " L "],
        Pricing = new ProductPricing("UZS", PricingStatus.Draft, [new PriceTier(1, 9, 130_000), new PriceTier(10, null, 100_000)], null),
    };

    [Fact]
    public async Task Create_ShouldAssignNextVariantAndNormalizeLists_WhenProductIsCreated()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;

        var first = await products.CreateAsync(Draft(), CancellationToken.None);
        var second = await products.CreateAsync(Draft(), CancellationToken.None);

        first.Document.Id.Should().Be("nimcha-v01");
        second.Document.Id.Should().Be("nimcha-v02");
        first.Document.Sizes.Should().BeEquivalentTo(["S", "M", "L"], options => options.WithStrictOrdering());
        first.Revision.Should().Be(1);
    }

    [Fact]
    public async Task Update_ShouldRequireCurrentRevisionAndRecordHistory_WhenProductIsUpdated()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;
        var created = await products.CreateAsync(Draft(), CancellationToken.None);

        var edited = await products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = 1, Tags = ["yangi"] }, CancellationToken.None);
        var stale = (await FluentActions.Awaiting(() =>
            products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = 1 }, CancellationToken.None)).Should().ThrowAsync<CatalogueRejection>()).Which;
        var history = await products.RevisionsAsync(created.Document.Id, CancellationToken.None);

        edited.Revision.Should().Be(2);
        stale.StatusCode.Should().Be(409);
        history.Select(h => h.Revision).Should().BeEquivalentTo([2, 1], options => options.WithStrictOrdering());
        history[0].Summary.Should().Contain("tags");
    }

    [Fact]
    public async Task Activation_ShouldNeedNameAndActiveImage_WhenProductIsActivated()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;

        var rejected = (await FluentActions.Awaiting(() =>
            products.CreateAsync(Draft() with { Status = ProductStatus.Active }, CancellationToken.None)).Should().ThrowAsync<CatalogueRejection>()).Which;
        var created = await products.CreateAsync(Draft(), CancellationToken.None);
        var withImage = await products.AddMediaAsync(created.Document.Id, CatalogueHarness.Png(10), "front.png", MediaRole.Cutout, 1, CancellationToken.None);
        var active = await products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = withImage.Revision, Status = ProductStatus.Active }, CancellationToken.None);

        rejected.Message.Should().Contain("image");
        active.Document.Status.Should().Be(ProductStatus.Active);
        active.Document.Media.Should().ContainSingle();
        active.Document.Media[0].HasPreview.Should().BeTrue();
    }

    [Fact]
    public async Task Update_ShouldRejectOverlappingPriceTiers_WhenTiersOverlap()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;
        var input = Draft() with
        {
            Pricing = new ProductPricing("UZS", PricingStatus.Draft, [new PriceTier(1, null, 130_000), new PriceTier(10, null, 100_000)], null),
        };

        var error = (await FluentActions.Awaiting(() => products.CreateAsync(input, CancellationToken.None)).Should().ThrowAsync<CatalogueRejection>()).Which;

        error.Message.Should().Contain("overlap");
    }

    [Fact]
    public async Task Duplicate_ShouldCreateDraftNextVariantWithDraftCopy_WhenProductIsDuplicated()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;
        var created = await products.CreateAsync(Draft() with
        {
            Locales = new ProductLocales(new ProductLocale(LocaleStatus.Approved, "Nimcha", null, null, null), ProductLocale.Empty(), ProductLocale.Empty()),
        }, CancellationToken.None);

        var copy = await products.DuplicateAsync(created.Document.Id, CancellationToken.None);

        copy.Document.Id.Should().Be("nimcha-v02");
        copy.Document.Status.Should().Be(ProductStatus.Draft);
        copy.Document.Locales.Uz.Status.Should().Be(LocaleStatus.Draft);
        copy.Document.Locales.Uz.Name.Should().Be("Nimcha");
    }

    [Fact]
    public async Task Restore_ShouldSaveOldSnapshotAsNewRevision_WhenRevisionIsRestored()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;
        var created = await products.CreateAsync(Draft(), CancellationToken.None);
        await products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = 1, Notes = "changed" }, CancellationToken.None);

        var restored = await products.RestoreAsync(created.Document.Id, 1, 2, CancellationToken.None);

        restored.Revision.Should().Be(3);
        restored.Document.Notes.Should().BeNull();
    }

    [Fact]
    public async Task BulkPricing_ShouldRoundAndMarkPricesDraft_WhenAdjusted()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;
        var created = await products.CreateAsync(Draft() with
        {
            Pricing = new ProductPricing("UZS", PricingStatus.Confirmed, [new PriceTier(1, 9, 130_000), new PriceTier(10, null, 100_000)], null),
        }, CancellationToken.None);

        var result = await products.AdjustPricesAsync(new BulkPricingInput([created.Document.Id, "missing"], "percent", 7.5m, 5000, "retail"), CancellationToken.None);
        var after = await products.GetAsync(created.Document.Id, CancellationToken.None);

        result.Changed.Should().Be(1);
        result.Skipped.Should().ContainSingle();
        after.Document.Pricing!.RetailPrice.Should().Be(140_000);
        after.Document.Pricing.WholesalePrice.Should().Be(100_000);
        after.Document.Pricing.Status.Should().Be(PricingStatus.Draft);
    }

    [Fact]
    public async Task BulkStatus_ShouldSkipProductsThatCannotBeActivated_WhenActivating()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;
        var created = await products.CreateAsync(Draft(), CancellationToken.None);

        var result = await products.SetStatusAsync(new BulkStatusInput([created.Document.Id], ProductStatus.Active), CancellationToken.None);

        result.Changed.Should().Be(0);
        result.Skipped.Single().Should().Contain("image");
    }

    [Fact]
    public async Task CopyGrid_ShouldSaveOnlyCopyAndCheckRevisionAndApprovalRules_WhenSaved()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;
        var created = await products.CreateAsync(Draft() with { Tags = ["kuz"] }, CancellationToken.None);
        var copy = new ProductLocales(new ProductLocale(LocaleStatus.Approved, "Nimcha", "Atlas", null, null),
            new ProductLocale(LocaleStatus.Draft, "Жилет", "Атлас", null, null), new ProductLocale(LocaleStatus.Draft, "Vest", null, null, null));

        var rows = await products.CopyRowsAsync(CancellationToken.None);
        var saved = await products.UpdateCopyAsync(created.Document.Id, new CopyInput(1, copy), CancellationToken.None);
        var stale = (await FluentActions.Awaiting(() => products.UpdateCopyAsync(created.Document.Id, new CopyInput(1, copy), CancellationToken.None)).Should().ThrowAsync<CatalogueRejection>()).Which;
        var invalid = (await FluentActions.Awaiting(() => products.UpdateCopyAsync(created.Document.Id,
            new CopyInput(2, copy with { Ru = new ProductLocale(LocaleStatus.Approved, null, null, null, null) }), CancellationToken.None)).Should().ThrowAsync<CatalogueRejection>()).Which;
        var after = await products.GetAsync(created.Document.Id, CancellationToken.None);

        rows.Should().ContainSingle();
        saved.Revision.Should().Be(2);
        stale.StatusCode.Should().Be(409);
        invalid.Message.Should().Contain("needs a name");
        after.Document.Locales.Ru.Name.Should().Be("Жилет");
        after.Document.Tags.Should().BeEquivalentTo(["kuz"], options => options.WithStrictOrdering());
        after.Document.Pricing!.RetailPrice.Should().Be(130_000);
    }

    [Fact]
    public async Task Stock_ShouldTrackSizesAndDrivePublicAvailability_WhenUpdated()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Products;
        var created = await products.CreateAsync(Draft(), CancellationToken.None);
        var withImage = await products.AddMediaAsync(created.Document.Id, CatalogueHarness.Png(33), "f.png", MediaRole.Cutout, 1, CancellationToken.None);
        await products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = withImage.Revision, Status = ProductStatus.Active, Locales = new ProductLocales(new ProductLocale(LocaleStatus.Approved, "Nimcha", "Atlas", null, null), ProductLocale.Empty(), ProductLocale.Empty()) }, CancellationToken.None);

        var untracked = (await harness.Feed.BuildAsync("uz", CancellationToken.None)).Products.Single();
        var tracked = await products.UpdateStockAsync(created.Document.Id, new StockInput(3, new Dictionary<string, int> { ["S"] = 2, ["L"] = 0 }, false), CancellationToken.None);
        var unknownSize = (await FluentActions.Awaiting(() =>
            products.UpdateStockAsync(created.Document.Id, new StockInput(4, new Dictionary<string, int> { ["XXL"] = 1 }, false), CancellationToken.None)).Should().ThrowAsync<CatalogueRejection>()).Which;
        var feed = (await harness.Feed.BuildAsync("uz", CancellationToken.None)).Products.Single();
        var soldOut = await products.UpdateStockAsync(created.Document.Id, new StockInput(4, new Dictionary<string, int>(), false), CancellationToken.None);
        var madeToOrder = await products.UpdateStockAsync(created.Document.Id, new StockInput(5, null, true), CancellationToken.None);
        var summary = (await products.ListAsync(new ProductFilter(null, null, null, null, null), CancellationToken.None)).Single();

        untracked.Availability.Should().Be("unknown");
        tracked.Stock.Should().BeEquivalentTo(new Dictionary<string, int> { ["S"] = 2, ["M"] = 0, ["L"] = 0 });
        unknownSize.Message.Should().Contain("does not offer");
        feed.Availability.Should().Be("in-stock");
        feed.AvailableSizes.Should().BeEquivalentTo(["S"], options => options.WithStrictOrdering());
        soldOut.Availability.Should().Be("sold-out");
        madeToOrder.Availability.Should().Be("made-to-order");
        summary.Availability.Should().Be("made-to-order");
    }
}
