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

    [Fact]
    public void FixedPrices_ValidateEditsWhilePreservingUnchangedLegacyPrices()
    {
        var product=Product();
        var legacy=product with {Document=product.Document with {Pricing=new("USD","confirmed",[new(1,null,25)],null)}};
        Action unchanged=()=>CommerceRules.ValidateEditedPrices(Snapshot(legacy),Snapshot(legacy));
        unchanged.Should().NotThrow();
        Action createLegacy=()=>CommerceRules.ValidateEditedPrices(Snapshot(legacy),null);
        createLegacy.Should().Throw<PreparationException>();
        var fixedPrice=product with {Document=product.Document with {Pricing=new("UZS","draft",[new(1,9,130000),new(10,null,100000)],null)}};
        Action edited=()=>CommerceRules.ValidateEditedPrices(Snapshot(fixedPrice),Snapshot(legacy));
        edited.Should().NotThrow();
        var wrongBounds=fixedPrice with {Document=fixedPrice.Document with {Pricing=fixedPrice.Document.Pricing! with {Tiers=[new(1,1,130000),new(10,null,100000)]}}};
        Action wrong=()=>CommerceRules.ValidateEditedPrices(Snapshot(wrongBounds),Snapshot(legacy));
        wrong.Should().Throw<PreparationException>();
    }
    [Fact]
    public void IncompleteFixedPrices_SaveOnlyAsDraft_AndNeverAppearAsPublicPrices()
    {
        var product=Product();
        var incomplete=product with {Document=product.Document with {Pricing=new("UZS","draft",[new(1,9,130000),new(10,null,0)],null)}};
        Action saveDraft=()=>CommerceRules.ValidateEditedPrices(Snapshot(incomplete),Snapshot(product));
        saveDraft.Should().NotThrow();
        var view=JsonSerializer.SerializeToElement(PublicProjection.Catalogue(Release(incomplete),"en")).GetProperty("products")[0];
        view.GetProperty("pricing").ValueKind.Should().Be(JsonValueKind.Null);
        view.GetProperty("price").ValueKind.Should().Be(JsonValueKind.Null);
        var confirmed=incomplete with {Document=incomplete.Document with {Pricing=incomplete.Document.Pricing! with {Status="confirmed"}}};
        Action confirmIncomplete=()=>CommerceRules.ValidateEditedPrices(Snapshot(confirmed),Snapshot(incomplete));
        confirmIncomplete.Should().Throw<PreparationException>().WithMessage("*positive UZS amounts for both price tiers*");
        var complete=confirmed with {Document=confirmed.Document with {Pricing=confirmed.Document.Pricing! with {Tiers=[new(1,9,130000),new(10,null,100000)]}}};
        Action confirmComplete=()=>CommerceRules.ValidateEditedPrices(Snapshot(complete),Snapshot(incomplete));
        confirmComplete.Should().NotThrow();
    }
    [Theory]
    [InlineData("UZS")]
    [InlineData("USD")]
    public void EditorSave_PreservesEquivalentDeserializedPrices_AndRevokesOnlyChangedPricing(string currency)
    {
        var product=Product();product=product with {Document=product.Document with {Pricing=new(currency,"confirmed",[new(1,9,130000),new(10,null,100000)],"Per piece")}};
        var serialized=JsonSerializer.Serialize(Snapshot(product));
        var previous=JsonSerializer.Deserialize<CommerceSnapshot>(serialized)!;
        var incoming=JsonSerializer.Deserialize<CommerceSnapshot>(serialized)!;
        incoming.Products[0].Document.Pricing!.Tiers.Should().NotBeSameAs(previous.Products[0].Document.Pricing!.Tiers);
        incoming=incoming with {Products=[incoming.Products[0] with {Document=incoming.Products[0].Document with {Notes="Unrelated private note"}}]};
        var restricted=CommerceRules.RestrictEditorApprovals(incoming,previous);
        restricted.Products[0].Document.Pricing!.Status.Should().Be("confirmed");
        restricted.Products[0].Document.Pricing!.Currency.Should().Be(currency);
        var changed=incoming.Products[0] with {Document=incoming.Products[0].Document with {Pricing=incoming.Products[0].Document.Pricing! with {Tiers=[new(1,9,140000),new(10,null,100000)]}}};
        var revised=CommerceRules.RestrictEditorApprovals(incoming with {Products=[changed]},previous);
        revised.Products[0].Document.Pricing!.Status.Should().Be("draft");
    }
    [Fact]
    public void NamedCatalogues_CoexistInOneLocale_AndFilterWebProductsWithoutLeakingCopy()
    {
        var product=Product(); var draft=product with {Document=product.Document with {Id="draft-vest",Locales=ProductLocales.Empty()}};
        var edition=new CatalogueEdition(Guid.NewGuid(),Guid.NewGuid(),1,"Summer","en","catalogue",[],null,true,DateTimeOffset.UtcNow)
        {Slug="summer",Web=new("grid",[new("one","Looks","grid",[product.Document.Id,draft.Document.Id]),new("empty","Hidden","grid",[draft.Document.Id]),new("inactive","Inactive","grid",[product.Document.Id]){IsActive=false}])};
        var snapshot=Snapshot(product) with {Products=[product,draft],CatalogueEditions=[edition,edition with {Id=Guid.NewGuid(),Slug="winter"},edition with {Id=Guid.NewGuid(),Slug="private",IsActive=false}]};
        CommerceRules.Validate(snapshot);
        var json=JsonSerializer.SerializeToElement(PublicProjection.Catalogue(Release(product) with {Snapshot=snapshot},"en"));
        json.GetProperty("edition").ValueKind.Should().Be(JsonValueKind.Null);
        var named=json.GetProperty("catalogues");named.GetArrayLength().Should().Be(2);
        var pages=named[0].GetProperty("web").GetProperty("pages");pages.GetArrayLength().Should().Be(1);
        pages[0].GetProperty("productIds").EnumerateArray().Select(id=>id.GetString()).Should().Equal(product.Document.Id);
        named[0].GetProperty("pdf").ValueKind.Should().Be(JsonValueKind.Null);
        var missing=JsonSerializer.SerializeToElement(PublicProjection.Catalogue(Release(product) with {Snapshot=snapshot},"fr"));
        missing.GetProperty("catalogues").GetArrayLength().Should().Be(0);
    }
    [Fact]
    public void StalePdf_DoesNotHideEligibleWebSiblings_AndNeverExposesUnconfirmedPdf()
    {
        var product=Product();var draft=product with {Document=product.Document with {Id="draft-vest",Locales=ProductLocales.Empty()}};var hash=new string('a',64);
        var edition=new CatalogueEdition(Guid.NewGuid(),Guid.NewGuid(),1,"Summer","en","catalogue",[new("mixed","Mixed",210,297,[product.Document.Id,draft.Document.Id],hash,hash)],hash,true,DateTimeOffset.UtcNow)
        {Slug="summer",Web=new("grid",[new("one","Looks","grid",[product.Document.Id,draft.Document.Id])])};
        var snapshot=Snapshot(product) with {Products=[product,draft],CatalogueEditions=[edition]};
        var json=JsonSerializer.SerializeToElement(PublicProjection.Catalogue(Release(product) with {Snapshot=snapshot},"en"));
        var named=json.GetProperty("catalogues");named.GetArrayLength().Should().Be(1);named[0].GetProperty("pdf").ValueKind.Should().Be(JsonValueKind.Null);
        named[0].GetProperty("web").GetProperty("pages")[0].GetProperty("productIds").EnumerateArray().Select(id=>id.GetString()).Should().Equal(product.Document.Id);
        CommerceRules.PdfEligible(edition,snapshot).Should().BeFalse();
    }
    [Theory]
    [InlineData("api")]
    [InlineData("../summer")]
    [InlineData("Summer")]
    [InlineData("summer--looks")]
    public void NamedCatalogue_RejectsReservedOrUnsafeSlug(string slug)
    {
        CatalogueDefinition.ValidSlug(slug).Should().BeFalse();
    }

}
