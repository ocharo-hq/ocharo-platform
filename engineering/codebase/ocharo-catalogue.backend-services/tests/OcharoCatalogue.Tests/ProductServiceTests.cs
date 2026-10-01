using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Tests;

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
    public async Task Create_assigns_next_variant_and_normalizes_lists()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();

        var first = await products.CreateAsync(Draft(), CancellationToken.None);
        var second = await products.CreateAsync(Draft(), CancellationToken.None);

        Assert.Equal("nimcha-v01", first.Document.Id);
        Assert.Equal("nimcha-v02", second.Document.Id);
        Assert.Equal(["S", "M", "L"], first.Document.Sizes);
        Assert.Equal(1, first.Revision);
    }

    [Fact]
    public async Task Update_requires_the_current_revision_and_records_history()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();
        var created = await products.CreateAsync(Draft(), CancellationToken.None);

        var edited = await products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = 1, Tags = ["yangi"] }, CancellationToken.None);
        var stale = await Assert.ThrowsAsync<CatalogueException>(() =>
            products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = 1 }, CancellationToken.None));
        var history = await products.RevisionsAsync(created.Document.Id, CancellationToken.None);

        Assert.Equal(2, edited.Revision);
        Assert.Equal(409, stale.StatusCode);
        Assert.Equal([2, 1], history.Select(h => h.Revision));
        Assert.Contains("tags", history[0].Summary);
    }

    [Fact]
    public async Task Activation_needs_a_name_and_an_active_image()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();

        var rejected = await Assert.ThrowsAsync<CatalogueException>(() =>
            products.CreateAsync(Draft() with { Status = ProductStatus.Active }, CancellationToken.None));
        var created = await products.CreateAsync(Draft(), CancellationToken.None);
        var withImage = await products.AddMediaAsync(created.Document.Id, CatalogueHarness.Png(10), "front.png", MediaRole.Cutout, 1, CancellationToken.None);
        var active = await products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = withImage.Revision, Status = ProductStatus.Active }, CancellationToken.None);

        Assert.Contains("image", rejected.Message);
        Assert.Equal(ProductStatus.Active, active.Document.Status);
        Assert.Single(active.Document.Media);
        Assert.True(active.Document.Media[0].HasPreview);
    }

    [Fact]
    public async Task Overlapping_price_tiers_are_rejected()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();
        var input = Draft() with
        {
            Pricing = new ProductPricing("UZS", PricingStatus.Draft, [new PriceTier(1, null, 130_000), new PriceTier(10, null, 100_000)], null),
        };

        var error = await Assert.ThrowsAsync<CatalogueException>(() => products.CreateAsync(input, CancellationToken.None));

        Assert.Contains("overlap", error.Message);
    }

    [Fact]
    public async Task Duplicate_creates_a_draft_next_variant_with_draft_copy()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();
        var created = await products.CreateAsync(Draft() with
        {
            Locales = new ProductLocales(new ProductLocale(LocaleStatus.Approved, "Nimcha", null, null, null), ProductLocale.Empty(), ProductLocale.Empty()),
        }, CancellationToken.None);

        var copy = await products.DuplicateAsync(created.Document.Id, CancellationToken.None);

        Assert.Equal("nimcha-v02", copy.Document.Id);
        Assert.Equal(ProductStatus.Draft, copy.Document.Status);
        Assert.Equal(LocaleStatus.Draft, copy.Document.Locales.Uz.Status);
        Assert.Equal("Nimcha", copy.Document.Locales.Uz.Name);
    }

    [Fact]
    public async Task Restore_saves_an_old_snapshot_as_a_new_revision()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();
        var created = await products.CreateAsync(Draft(), CancellationToken.None);
        await products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = 1, Notes = "changed" }, CancellationToken.None);

        var restored = await products.RestoreAsync(created.Document.Id, 1, 2, CancellationToken.None);

        Assert.Equal(3, restored.Revision);
        Assert.Null(restored.Document.Notes);
    }

    [Fact]
    public async Task Bulk_pricing_rounds_and_marks_prices_draft()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();
        var created = await products.CreateAsync(Draft() with
        {
            Pricing = new ProductPricing("UZS", PricingStatus.Confirmed, [new PriceTier(1, 9, 130_000), new PriceTier(10, null, 100_000)], null),
        }, CancellationToken.None);

        var result = await products.AdjustPricesAsync(new BulkPricingInput([created.Document.Id, "missing"], "percent", 7.5m, 5000, "retail"), CancellationToken.None);
        var after = await products.GetAsync(created.Document.Id, CancellationToken.None);

        Assert.Equal(1, result.Changed);
        Assert.Single(result.Skipped);
        Assert.Equal(140_000, after.Document.Pricing!.RetailPrice);
        Assert.Equal(100_000, after.Document.Pricing.WholesalePrice);
        Assert.Equal(PricingStatus.Draft, after.Document.Pricing.Status);
    }

    [Fact]
    public async Task Bulk_status_skips_products_that_cannot_be_activated()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();
        var created = await products.CreateAsync(Draft(), CancellationToken.None);

        var result = await products.SetStatusAsync(new BulkStatusInput([created.Document.Id], ProductStatus.Active), CancellationToken.None);

        Assert.Equal(0, result.Changed);
        Assert.Contains("image", result.Skipped.Single());
    }

    [Fact]
    public async Task Copy_grid_saves_only_copy_and_checks_revision_and_approval_rules()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();
        var created = await products.CreateAsync(Draft() with { Tags = ["kuz"] }, CancellationToken.None);
        var copy = new ProductLocales(new ProductLocale(LocaleStatus.Approved, "Nimcha", "Atlas", null, null),
            new ProductLocale(LocaleStatus.Draft, "Жилет", "Атлас", null, null), new ProductLocale(LocaleStatus.Draft, "Vest", null, null, null));

        var rows = await products.CopyRowsAsync(CancellationToken.None);
        var saved = await products.UpdateCopyAsync(created.Document.Id, new CopyInput(1, copy), CancellationToken.None);
        var stale = await Assert.ThrowsAsync<CatalogueException>(() => products.UpdateCopyAsync(created.Document.Id, new CopyInput(1, copy), CancellationToken.None));
        var invalid = await Assert.ThrowsAsync<CatalogueException>(() => products.UpdateCopyAsync(created.Document.Id,
            new CopyInput(2, copy with { Ru = new ProductLocale(LocaleStatus.Approved, null, null, null, null) }), CancellationToken.None));
        var after = await products.GetAsync(created.Document.Id, CancellationToken.None);

        Assert.Single(rows);
        Assert.Equal(2, saved.Revision);
        Assert.Equal(409, stale.StatusCode);
        Assert.Contains("needs a name", invalid.Message);
        Assert.Equal("Жилет", after.Document.Locales.Ru.Name);
        Assert.Equal(["kuz"], after.Document.Tags);
        Assert.Equal(130_000, after.Document.Pricing!.RetailPrice);
    }

    [Fact]
    public async Task Stock_tracks_sizes_and_drives_public_availability()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        var products = harness.Get<ProductService>();
        var created = await products.CreateAsync(Draft(), CancellationToken.None);
        var withImage = await products.AddMediaAsync(created.Document.Id, CatalogueHarness.Png(33), "f.png", MediaRole.Cutout, 1, CancellationToken.None);
        await products.UpdateAsync(created.Document.Id, Draft() with { ExpectedRevision = withImage.Revision, Status = ProductStatus.Active, Locales = new ProductLocales(new ProductLocale(LocaleStatus.Approved, "Nimcha", "Atlas", null, null), ProductLocale.Empty(), ProductLocale.Empty()) }, CancellationToken.None);

        var untracked = (await harness.Get<PublicCatalogueService>().BuildAsync("uz", CancellationToken.None)).Products.Single();
        var tracked = await products.UpdateStockAsync(created.Document.Id, new StockInput(3, new Dictionary<string, int> { ["S"] = 2, ["L"] = 0 }, false), CancellationToken.None);
        var unknownSize = await Assert.ThrowsAsync<CatalogueException>(() =>
            products.UpdateStockAsync(created.Document.Id, new StockInput(4, new Dictionary<string, int> { ["XXL"] = 1 }, false), CancellationToken.None));
        var feed = (await harness.Get<PublicCatalogueService>().BuildAsync("uz", CancellationToken.None)).Products.Single();
        var soldOut = await products.UpdateStockAsync(created.Document.Id, new StockInput(4, new Dictionary<string, int>(), false), CancellationToken.None);
        var madeToOrder = await products.UpdateStockAsync(created.Document.Id, new StockInput(5, null, true), CancellationToken.None);
        var summary = (await products.ListAsync(new ProductFilter(null, null, null, null, null), CancellationToken.None)).Single();

        Assert.Equal("unknown", untracked.Availability);
        Assert.Equal(new Dictionary<string, int> { ["S"] = 2, ["M"] = 0, ["L"] = 0 }, tracked.Stock);
        Assert.Contains("does not offer", unknownSize.Message);
        Assert.Equal("in-stock", feed.Availability);
        Assert.Equal(["S"], feed.AvailableSizes);
        Assert.Equal("sold-out", soldOut.Availability);
        Assert.Equal("made-to-order", madeToOrder.Availability);
        Assert.Equal("made-to-order", summary.Availability);
    }
}
