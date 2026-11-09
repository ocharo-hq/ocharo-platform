using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Application.Catalogue.Importing.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Exporting.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator;

namespace OcharoCatalogue.Tests.Integration;

public sealed class ImportExportTests
{
    [Fact]
    public async Task Import_ShouldMapWorkspaceRecordsWithImagesProvenanceAndReviewState_WhenImported()
    {
        var source = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-source", Guid.NewGuid().ToString("N"));
        CatalogueHarness.WriteWorkspaceProduct(source, "nimcha-v06", "nimcha", 110_000, 60_000, 20);
        CatalogueHarness.WriteWorkspaceProduct(source, "baska-v02", "baska", 180_000, 130_000, 40);
        Directory.CreateDirectory(Path.Combine(source, "review"));
        await using var harness = await CatalogueHarness.CreateAsync(source);
        var importer = harness.Importer;

        var report = await importer.ImportAsync(new ImportInput(null), CancellationToken.None);
        var again = await importer.ImportAsync(new ImportInput(null), CancellationToken.None);
        var product = await harness.Products.GetAsync("nimcha-v06", CancellationToken.None);

        report.Scanned.Should().Be(2);
        report.Created.Should().Be(2);
        report.ImagesStored.Should().Be(4);
        report.Warnings.Should().Contain(w => w.Contains("missing image"));
        again.Unchanged.Should().Be(2);
        product.Document.Status.Should().Be(ProductStatus.Active);
        product.Document.Locales.Uz.Status.Should().Be(LocaleStatus.Draft);
        product.Document.Pricing!.Status.Should().Be(PricingStatus.Confirmed);
        product.Document.Pricing.RetailPrice.Should().Be(110_000);
        product.Document.Media.Select(m => m.Role).Should().BeEquivalentTo([MediaRole.Cutout, MediaRole.Model], options => options.WithStrictOrdering());
        product.Document.SourceVerbatim.Should().Be("Material: sintetika");
        product.Document.Source!.Sha256!.Length.Should().Be(64);
        product.Document.Issues.Select(i => i.Status).Should().BeEquivalentTo([IssueStatus.Open, IssueStatus.Resolved], options => options.WithStrictOrdering());
        product.Document.Media.All(m => m.HasPreview).Should().BeTrue();
        Directory.Delete(source, true);
    }

    [Fact]
    public async Task PublicFeed_ShouldListOnlyActiveProductsAndPublishedCollections_WhenBuilt()
    {
        var source = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-source", Guid.NewGuid().ToString("N"));
        CatalogueHarness.WriteWorkspaceProduct(source, "nimcha-v06", "nimcha", 110_000, 60_000, 60);
        await using var harness = await CatalogueHarness.CreateAsync(source);
        await harness.Importer.ImportAsync(new ImportInput(null), CancellationToken.None);
        var products = harness.Products;
        var imported = await products.GetAsync("nimcha-v06", CancellationToken.None);
        await products.UpdateAsync(imported.Document.Id, new ProductInput
        {
            ExpectedRevision = imported.Revision, GarmentType = imported.Document.GarmentType,
            VariantCode = imported.Document.VariantCode, Status = ProductStatus.Active,
            Locales = imported.Document.Locales with { Ru = imported.Document.Locales.Ru with { Status = LocaleStatus.Approved } },
            Pricing = imported.Document.Pricing,
            Issues = imported.Document.Issues.Select(issue => issue with { Status = IssueStatus.Resolved }).ToList(),
        }, CancellationToken.None);
        var draft = await products.CreateAsync(new ProductInput { GarmentType = "korset" }, CancellationToken.None);
        var collections = harness.Collections;
        await collections.SaveAsync(null, new CollectionInput(null, "kuz-2026", new CollectionNames("Kuz 2026", "Осень 2026", null), null,
            ["nimcha-v06", draft.Document.Id], true, 0), CancellationToken.None);
        await collections.SaveAsync(null, new CollectionInput(null, "hidden", new CollectionNames("Yashirin", null, null), null, [], false, 1), CancellationToken.None);

        var feed = await harness.Feed.BuildAsync("ru", CancellationToken.None);

        var product = feed.Products.Should().ContainSingle().Subject;
        product.Id.Should().Be("nimcha-v06");
        product.Name.Should().Be("Жилет");
        product.GarmentName.Should().Be("Жилет");
        product.Pricing!.Retail.Should().Be(110_000);
        product.Images[0].Url.Should().StartWith("/api/catalogue/media/");
        var collection = feed.Collections.Should().ContainSingle().Subject;
        collection.Name.Should().Be("Осень 2026");
        collection.ProductIds.Should().BeEquivalentTo(["nimcha-v06"], options => options.WithStrictOrdering());
        Directory.Delete(source, true);
    }

    [Fact]
    public async Task WorkspaceSchemaExport_ShouldRoundTripPricesAndReviewState_WhenExportedAndImported()
    {
        var source = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-source", Guid.NewGuid().ToString("N"));
        CatalogueHarness.WriteWorkspaceProduct(source, "kostyum-v01", "kostyum", 250_000, 180_000, 80);
        await using var harness = await CatalogueHarness.CreateAsync(source);
        await harness.Importer.ImportAsync(new ImportInput(null), CancellationToken.None);
        var product = await harness.Products.GetAsync("kostyum-v01", CancellationToken.None);

        var json = WorkspaceSchemaMapper.ToWorkspaceSchema(product.Document, product.UpdatedAt);
        var csv = System.Text.Encoding.UTF8.GetString(await harness.Exports.CsvAsync(CancellationToken.None));

        json["pricing"]!["status"]!.GetValue<string>().Should().Be("user_confirmed");
        json["pricing"]!["tiers"]![0]!["unit_price"]!.GetValue<long>().Should().Be(250_000);
        json["review"]!["status"]!.GetValue<string>().Should().Be("TBD");
        json["assets"]!["cutout_images"]![0]!.GetValue<string>().Should().Be("references/och-kostyum-v01-front-transparent.png");
        csv.Should().Contain("kostyum-v01,kostyum,v01,active,Nimcha");
        Directory.Delete(source, true);
    }

    [Fact]
    public async Task Csv_ShouldNeutralizeFormulaCells_WhenExported()
    {
        await using var harness = await CatalogueHarness.CreateAsync();
        await harness.Products.CreateAsync(new ProductInput
        {
            GarmentType = "abaya",
            Locales = new ProductLocales(new ProductLocale(LocaleStatus.Draft, "=HYPERLINK(1)", null, null, null), ProductLocale.Empty(), ProductLocale.Empty()),
        }, CancellationToken.None);

        var csv = System.Text.Encoding.UTF8.GetString(await harness.Exports.CsvAsync(CancellationToken.None));

        csv.Should().Contain("'=HYPERLINK(1)");
    }
}
