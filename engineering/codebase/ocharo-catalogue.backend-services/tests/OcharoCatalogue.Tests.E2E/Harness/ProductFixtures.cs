using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>A product document ready to publish (approved Uzbek and French copy, confirmed UZS prices, one active image) and
/// the pieces tests vary.</summary>
public static class ProductFixtures
{
    public static ProductLocale Approved(string name) => new(LocaleStatus.Approved, name, "Adras", null, "A woven layer.");
    public static ProductPricing Prices(string status = PricingStatus.Confirmed) => new("UZS", status, [new(1, 9, 130_000), new(10, null, 100_000)], null);
    public static ProductDocument Ready() => new()
    {
        Id = "nimcha-v01", GarmentType = "nimcha", VariantCode = "v01", Status = ProductStatus.Active,
        Locales = new(Approved("Nimcha"), ProductLocale.Empty(), new(LocaleStatus.Draft, "Unreviewed vest", null, null, null))
        {
            Additional = new Dictionary<string, ProductLocale> { ["fr"] = Approved("Gilet"), ["ar"] = new(LocaleStatus.Draft, "Unreviewed Arabic", null, null, null) },
        },
        Pricing = Prices(),
        Media = [new("front", MediaRole.Cutout, "front.png", "image/png", 200, new string('a', 64), 60, 90, false, "Front", null)],
    };
}
