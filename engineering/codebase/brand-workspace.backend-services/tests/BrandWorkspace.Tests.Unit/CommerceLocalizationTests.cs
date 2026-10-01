using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Commerce;
using OcharoBrand.Domain.Brand;
using OcharoCatalogue.Domain.Catalogue.Models;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>Checks exact language publication, additive source preservation and product-owned media groups.</summary>
public sealed class CommerceLocalizationTests
{
    private static CommerceProduct Product() => new(new ProductDocument
    {
        Id = "nimcha-v01", GarmentType = "nimcha", VariantCode = "v01", Status = "active",
        Locales = new(new("approved", "Nimcha", "Ipak", null, null), ProductLocale.Empty(), new("approved", "Vest", "Silk", null, null)),
        Media = [new("front", "cutout", "front.png", "image/png", 1, new string('a', 64), 1, 1, false, null, null)]
    });
    private static CommerceSnapshot Snapshot(CommerceProduct? product = null) => new()
    {
        Slug = "ocharo", Identity = new BrandIdentity { Name = "Ocharo" }, Products = [product ?? Product()]
    };
    private static CommerceRelease Release(CommerceProduct product) => new(Guid.NewGuid(), Guid.NewGuid(), 1, 1, DateTimeOffset.UtcNow, "Test", Snapshot(product));

    [Fact]
    public void ExtendedLocale_RoundtripsWithoutMovingOriginalCopyOrMedia()
    {
        var product = Product() with { Translations = new Dictionary<string, ProductLocale> { ["fr"] = new("approved", "Gilet", "Soie", null, null) },
            MediaGroups = [new("front-back", "Product views", "product", ["front"])] };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var original = JsonSerializer.Serialize(Snapshot(product), options);
        var restored = JsonSerializer.Deserialize<CommerceSnapshot>(original, options)!;
        CommerceRules.Validate(restored);
        restored.Products[0].Document.Locales.Uz.Name.Should().Be("Nimcha");
        CatalogueLanguages.Copy(restored.Products[0], "fr").Name.Should().Be("Gilet");
        restored.Products[0].MediaGroups![0].MediaIds.Should().Equal("front");
        restored.Products[0].Document.Media[0].Sha256.Should().Be(new string('a', 64));
    }

    [Fact]
    public void MissingExtendedTranslation_DoesNotPublishAnotherLanguageAsFrench()
    {
        var result = JsonSerializer.SerializeToElement(PublicProjection.Catalogue(Release(Product()), "fr"));
        result.GetProperty("products").GetArrayLength().Should().Be(0);
        CatalogueLanguages.Copy(Product(), "fr").Status.Should().Be("draft");
        CatalogueLanguages.Copy(Product(), "fr").Name.Should().BeNull();
    }

    [Fact]
    public void ApprovedExtendedTranslation_UsesItsExactCopyAndHidesPrivateFields()
    {
        var product = Product() with { Translations = new Dictionary<string, ProductLocale> { ["fr"] = new("approved", "Gilet", "Soie", null, "Description française") } };
        var json = JsonSerializer.SerializeToElement(PublicProjection.Catalogue(Release(product), "fr"));
        var view = json.GetProperty("products")[0];
        view.GetProperty("title").GetString().Should().Be("Gilet");
        view.GetProperty("fabric").GetString().Should().Be("Soie");
        view.TryGetProperty("notes", out _).Should().BeFalse();
        view.TryGetProperty("translations", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("uz")]
    [InlineData("xx")]
    [InlineData("zh")]
    public void AdditionalTranslations_RejectDuplicateCoreOrUnsupportedKeys(string language)
    {
        var product = Product() with { Translations = new Dictionary<string, ProductLocale> { [language] = new("draft", "Title", null, null, null) } };
        Action action = () => CommerceRules.Validate(Snapshot(product));
        action.Should().Throw<PreparationException>();
    }

    [Fact]
    public void EditingTranslatedCopy_RevokesEditorApprovalWithoutChangingOtherLanguages()
    {
        var product = Product() with { Translations = new Dictionary<string, ProductLocale> { ["fr"] = new("approved", "Gilet", null, null, null) } };
        var updated = product with { Translations = new Dictionary<string, ProductLocale> { ["fr"] = new("approved", "Gilet nouveau", null, null, null) } };
        var restricted = CommerceRules.RestrictEditorApprovals(Snapshot(updated), Snapshot(product));
        CatalogueLanguages.Copy(restricted.Products[0], "fr").Status.Should().Be("draft");
        CatalogueLanguages.Copy(restricted.Products[0], "uz").Status.Should().Be("approved");
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("duplicate")]
    [InlineData("reference")]
    [InlineData("category")]
    public void MediaGroups_RejectForeignDuplicatePrivateOrUnknownSelections(string fault)
    {
        var product = Product();
        if (fault == "reference") product = product with { Document = product.Document with { Media = [product.Document.Media[0] with { Role = "reference" }] } };
        product = product with { MediaGroups = [new("photos", "Photos", fault == "category" ? "other" : "product", fault == "foreign" ? ["another-product"] : fault == "duplicate" ? ["front", "front"] : ["front"])] };
        Action action = () => CommerceRules.Validate(Snapshot(product));
        action.Should().Throw<PreparationException>();
    }

    [Fact]
    public void Editions_AcceptAdditionalLanguagesWithoutChangingSavedIdentity()
    {
        var hash = new string('a', 64);
        var edition = new CatalogueEdition(Guid.NewGuid(), Guid.NewGuid(), 1, "Gilet", "fr", "catalogue", [new("cover", "Cover", 210, 297, [], hash, hash)], hash, false, DateTimeOffset.UtcNow);
        var snapshot = Snapshot() with { CatalogueEditions = new[] { "uz", "ru", "en", "fr" }.Select(locale => edition with { Id = Guid.NewGuid(), Locale = locale }).ToArray() };
        Action action = () => CommerceRules.Validate(snapshot);
        action.Should().NotThrow();
    }
    [Fact]
    public void PublicLocaleChoices_UseOnlyApprovedEditionLanguagesFromThisRelease()
    {
        var hash = new string('a', 64);
        var edition = new CatalogueEdition(Guid.NewGuid(), Guid.NewGuid(), 1, "Gilet", "fr", "catalogue", [new("cover", "Cover", 210, 297, [], hash, hash)], hash, true, DateTimeOffset.UtcNow);
        var release = Release(Product()) with { Snapshot = Snapshot() with { CatalogueEditions = [
            edition,
            edition with { Id = Guid.NewGuid(), Locale = "it", ApprovedForPublication = false },
            edition with { Id = Guid.NewGuid(), Locale = "uz" },
        ] } };
        var json = JsonSerializer.SerializeToElement(PublicProjection.Catalogue(release, "fr"));
        json.GetProperty("availableLocales").EnumerateArray().Select(item => item.GetString()).Should().Equal("fr", "uz");
    }

}
