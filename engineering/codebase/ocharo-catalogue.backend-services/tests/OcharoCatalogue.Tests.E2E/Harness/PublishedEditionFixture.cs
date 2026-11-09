using System.Text.Json;
using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using SkiaSharp;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>Fills a catalogue the way the dev store holds one: a product with two revisions and its image, saved
/// settings, a collection, and the published web + PDF edition <c>wave1-check</c> with its artifacts.</summary>
public static class PublishedEditionFixture
{
    public const string Slug = "wave1-check";
    public const string Locale = "en";
    public const string ProductId = "nimcha-v01";

    /// <summary>Seeds the catalogue and publishes the edition; returns its release and the PDF it serves.</summary>
    public static async Task<(Guid DesignId, Guid ReleaseId, byte[] Pdf)> PublishAsync(CatalogueHarness harness)
    {
        var ct = CancellationToken.None;
        var media = await harness.Get<IMediaStore>().StoreAsync(CatalogueHarness.Png(81), ct);
        var copy = new ProductLocale("approved", ProductId, "Silk", null, null);
        var product = new ProductDocument
        {
            Id = ProductId, GarmentType = "nimcha", VariantCode = "v01", Status = "active", Locales = new(copy, copy, copy),
            Media = [new("image", "model", "model.png", "image/png", media.ByteLength, media.Sha256, media.Width, media.Height, true, null, null)],
            Pricing = new("UZS", "confirmed", [new(1, 9, 250000), new(10, null, 190000)], null),
        };
        var repository = harness.Get<ICatalogueRepository>();
        await repository.InsertProductAsync(ProductProjections.ToRecord(product, 1, harness.Clock.GetUtcNow(), harness.Clock.GetUtcNow()), "Seed", ct);
        harness.Clock.Advance(TimeSpan.FromMinutes(5));
        var edited = product with { Notes = "Checked for wave 1" };
        await repository.UpdateProductAsync(ProductProjections.ToRecord(edited, 2, harness.Clock.GetUtcNow(), harness.Clock.GetUtcNow()), 1, "Edited", ct);
        await harness.Settings.SaveAsync(CatalogueSettings.Default() with { PriceNote = "Prices in UZS" }, 0, ct);
        await harness.Collections.SaveAsync(null, new CollectionInput(null, "wave-1", new CollectionNames("To'lqin 1", null, null), null, [ProductId], false, 0), ct);

        var snapshot = JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 1, kind = "catalogue", templateId = "catalogue-blank", locale = Locale,
            catalogue = new
            {
                slug = Slug, description = "Wave 1 check", isActive = true, outputs = new { web = true, pdf = true },
                web = new { theme = "editorial", pages = new[] { new { id = "web-one", title = "Selected pieces", layout = "grid", isActive = true, productIds = new[] { ProductId } } } },
            },
            productRevisions = new Dictionary<string, int> { [ProductId] = 2 },
            pages = new[]
            {
                new { id = "cover", title = "cover", width = 210, height = 297, background = "#ffffff", isActive = true, productIds = Array.Empty<string>(), elements = Array.Empty<object>() },
            },
        }, CatalogueJson.Options);
        var design = await harness.Designs.SaveAsync(null, new CatalogueDesignInput("Wave 1 check", snapshot), ct);
        var preparation = JsonSerializer.SerializeToElement(await harness.Releases.PreparationAsync(design.Id, ct), CatalogueJson.Options);
        var pdf = Pdf();
        var files = new Dictionary<string, byte[]>
        {
            ["catalogue.pdf"] = pdf,
            ["page-cover.pdf"] = Pdf(),
            ["page-cover.png"] = CatalogueHarness.Png(81, 420, 594),
        };
        var prepared = await harness.Releases.PrepareAsync(design.Id, 1, preparation.GetProperty("fingerprint").GetString()!, files, ct);
        await harness.Releases.PublishAsync(design.Id, prepared.Id, 1, ct);
        return (design.Id, prepared.Id, pdf);
    }

    /// <summary>A one-page A4 PDF.</summary>
    private static byte[] Pdf()
    {
        using var output = new MemoryStream();
        using (var document = SKDocument.CreatePdf(output))
        {
            var canvas = document.BeginPage(210 * 72 / 25.4f, 297 * 72 / 25.4f);
            canvas.Clear(SKColors.White);
            document.EndPage();
            document.Close();
        }

        return output.ToArray();
    }
}
