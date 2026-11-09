using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Designs.Models;

/// <summary>Represents what the designer composes from: every product in catalogue order, and the settings.</summary>
public sealed record DesignSource(IReadOnlyList<ProductDetail> Products, CatalogueSettings Settings);
