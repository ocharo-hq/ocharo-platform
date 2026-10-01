using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue;

namespace OcharoCatalogue.Tests;

public sealed class ImportExportTests
{
    [Fact]
    public async Task Import_maps_workspace_records_with_images_provenance_and_review_state()
    {
        var source = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-source", Guid.NewGuid().ToString("N"));
        CatalogueHarness.WriteWorkspaceProduct(source, "nimcha-v06", "nimcha", 110_000, 60_000, 20);
        CatalogueHarness.WriteWorkspaceProduct(source, "baska-v02", "baska", 180_000, 130_000, 40);
        Directory.CreateDirectory(Path.Combine(source, "review"));
        await using var harness = await CatalogueHarness.CreateAsync(source);
        var importer = harness.Get<WorkspaceCatalogueImporter>();

        var report = await importer.ImportAsync(new ImportInput(null), CancellationToken.None);
        var again = await importer.ImportAsync(new ImportInput(null), CancellationToken.None);
        var product = await harness.Get<ProductService>().GetAsync("nimcha-v06", CancellationToken.None);

        Assert.Equal(2, report.Scanned);
        Assert.Equal(2, report.Created);
        Assert.Equal(4, report.ImagesStored);
        Assert.Contains(report.Warnings, w => w.Contains("missing image"));
        Assert.Equal(2, again.Unchanged);
        Assert.Equal(ProductStatus.Active, product.Document.Status);
        Assert.Equal(LocaleStatus.Draft, product.Document.Locales.Uz.Status);
        Assert.Equal(PricingStatus.Confirmed, product.Document.Pricing!.Status);
        Assert.Equal(110_000, product.Document.Pricing.RetailPrice);
        Assert.Equal([MediaRole.Cutout, MediaRole.Model], product.Document.Media.Select(m => m.Role));
        Assert.Equal("Material: sintetika", product.Document.SourceVerbatim);
        Assert.Equal(64, product.Document.Source!.Sha256!.Length);
        Assert.Equal([IssueStatus.Open, IssueStatus.Resolved], product.Document.Issues.Select(i => i.Status));
        Assert.True(product.Document.Media.All(m => m.HasPreview));
        Directory.Delete(source, true);
    }

    [Fact]
    public async Task Public_feed_lists_only_active_products_and_published_collections()
    {
        var source = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-source", Guid.NewGuid().ToString("N"));
        CatalogueHarness.WriteWorkspaceProduct(source, "nimcha-v06", "nimcha", 110_000, 60_000, 60);
        await using var harness = await CatalogueHarness.CreateAsync(source);
        await harness.Get<WorkspaceCatalogueImporter>().ImportAsync(new ImportInput(null), CancellationToken.None);
        var products = harness.Get<ProductService>();
        var draft = await products.CreateAsync(new ProductInput { GarmentType = "korset" }, CancellationToken.None);
        var collections = harness.Get<CollectionService>();
        await collections.SaveAsync(null, new CollectionInput(null, "kuz-2026", new CollectionNames("Kuz 2026", "Осень 2026", null), null,
            ["nimcha-v06", draft.Document.Id], true, 0), CancellationToken.None);
        await collections.SaveAsync(null, new CollectionInput(null, "hidden", new CollectionNames("Yashirin", null, null), null, [], false, 1), CancellationToken.None);

        var feed = await harness.Get<PublicCatalogueService>().BuildAsync("ru", CancellationToken.None);

        var product = Assert.Single(feed.Products);
        Assert.Equal("nimcha-v06", product.Id);
        Assert.Equal("Жилет", product.Name);
        Assert.Equal("Жилет", product.GarmentName);
        Assert.Equal(110_000, product.Pricing!.Retail);
        Assert.StartsWith("/api/catalogue/media/", product.Images[0].Url);
        var collection = Assert.Single(feed.Collections);
        Assert.Equal("Осень 2026", collection.Name);
        Assert.Equal(["nimcha-v06"], collection.ProductIds);
        Directory.Delete(source, true);
    }

    [Fact]
    public async Task Workspace_schema_export_round_trips_prices_and_review_state()
    {
        var source = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-source", Guid.NewGuid().ToString("N"));
        CatalogueHarness.WriteWorkspaceProduct(source, "kostyum-v01", "kostyum", 250_000, 180_000, 80);
        await using var harness = await CatalogueHarness.CreateAsync(source);
        await harness.Get<WorkspaceCatalogueImporter>().ImportAsync(new ImportInput(null), CancellationToken.None);
        var product = await harness.Get<ProductService>().GetAsync("kostyum-v01", CancellationToken.None);

        var json = CatalogueExportService.ToWorkspaceSchema(product.Document, product.UpdatedAt);
        var csv = System.Text.Encoding.UTF8.GetString(await harness.Get<CatalogueExportService>().CsvAsync(CancellationToken.None));

        Assert.Equal("user_confirmed", json["pricing"]!["status"]!.GetValue<string>());
        Assert.Equal(250_000, json["pricing"]!["tiers"]![0]!["unit_price"]!.GetValue<long>());
        Assert.Equal("TBD", json["review"]!["status"]!.GetValue<string>());
        Assert.Equal("references/och-kostyum-v01-front-transparent.png", json["assets"]!["cutout_images"]![0]!.GetValue<string>());
        Assert.Contains("kostyum-v01,kostyum,v01,active,Nimcha", csv);
        Directory.Delete(source, true);
    }

    [Fact]
    public async Task Csv_neutralizes_formula_cells()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        await harness.Get<ProductService>().CreateAsync(new ProductInput
        {
            GarmentType = "abaya",
            Locales = new ProductLocales(new ProductLocale(LocaleStatus.Draft, "=HYPERLINK(1)", null, null, null), ProductLocale.Empty(), ProductLocale.Empty()),
        }, CancellationToken.None);

        var csv = System.Text.Encoding.UTF8.GetString(await harness.Get<CatalogueExportService>().CsvAsync(CancellationToken.None));

        Assert.Contains("'=HYPERLINK(1)", csv);
    }
}
