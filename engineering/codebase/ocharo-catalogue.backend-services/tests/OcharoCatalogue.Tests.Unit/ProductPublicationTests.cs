using System.Text.Json.Nodes;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoCatalogue.Tests.Unit;

/// <summary>What the public catalogue may expose of a product: stored JSON compatibility, locale eligibility and public prices.</summary>
public sealed class ProductPublicationTests
{
    [Fact]
    public void LegacyJson_ShouldStayReadableAndAdditionalLanguagesRoundTripWithoutFallback_WhenRead()
    {
        var node = JsonNode.Parse(CatalogueJson.Write(ProductFixtures.Ready()))!;
        node["locales"]!.AsObject().Remove("additional");
        node["media"]![0]!.AsObject().Remove("isActive");
        node["media"]![0]!.AsObject().Remove("group");
        var old = CatalogueJson.Read<ProductDocument>(node.ToJsonString());
        old.Media[0].IsActive.Should().BeTrue();
        old.Locales.Additional.Should().BeEmpty();
        old.Locales.For("fr").Name.Should().BeNull();
        old.Locales.For("uz").Name.Should().Be("Nimcha");
        CatalogueJson.Read<ProductDocument>(CatalogueJson.Write(ProductFixtures.Ready())).Locales.For("fr").Name.Should().Be("Gilet");
        CatalogueLocales.All.Count.Should().Be(25);
        CatalogueLocales.Direction("ar").Should().Be("rtl");
        CatalogueLocales.Direction("fr").Should().Be("ltr");
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("missing-language")]
    [InlineData("unapproved-language")]
    [InlineData("empty-name")]
    [InlineData("no-active-image")]
    [InlineData("open-review")]
    public void Eligibility_ShouldRejectProduct_WhenBoundaryIsUnconfirmed(string condition)
    {
        var product = ProductFixtures.Ready(); var locale = "fr";
        product = condition switch
        {
            "draft" => product with { Status = ProductStatus.Draft },
            "empty-name" => product with { Locales = product.Locales with { Additional = new Dictionary<string, ProductLocale> { ["fr"] = ProductFixtures.Approved(" ") } } },
            "no-active-image" => product with { Media = [product.Media[0] with { IsActive = false }] },
            "open-review" => product with { Issues = [new("review", "fabric", IssueStatus.Open, "Confirm fabric")] },
            _ => product,
        };
        if (condition == "missing-language") locale = "de";
        if (condition == "unapproved-language") locale = "en";
        ProductPublicationEligibility.Eligible(product, locale).Should().BeFalse();
        FluentActions.Invoking(() => PublicProductMapper.ToPublic(product, 1, DateTimeOffset.UtcNow, locale, new Dictionary<string, GarmentTypeDefinition>())).Should().Throw<AppException>();
    }

    [Fact]
    public void PublicPrices_ShouldBeOnlyConfirmedValidUzsPrices_WhenMapped()
    {
        foreach (var pricing in new[] { ProductFixtures.Prices(PricingStatus.Draft), ProductFixtures.Prices() with { Currency = "USD" }, ProductFixtures.Prices() with { Tiers = [new(1, null, 130_000), new(10, null, 100_000)] } })
        {
            var product = PublicProductMapper.ToPublic(ProductFixtures.Ready() with { Pricing = pricing }, 1, DateTimeOffset.UtcNow, "fr", new Dictionary<string, GarmentTypeDefinition>());
            product.Pricing.Should().BeNull();
        }
    }
}
