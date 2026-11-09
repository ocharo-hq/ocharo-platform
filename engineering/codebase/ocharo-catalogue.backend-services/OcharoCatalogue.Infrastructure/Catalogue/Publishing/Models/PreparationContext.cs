using System.Text.Json;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.Models;

/// <summary>
/// Represents a saved design read for preparation, with the products it references and their fingerprint.
/// </summary>
/// <param name="Design">The design row.</param>
/// <param name="Snapshot">The validated snapshot.</param>
/// <param name="Definition">The validated catalogue metadata.</param>
/// <param name="Rows">Every product row.</param>
/// <param name="Products">Every product document by id.</param>
/// <param name="References">The products the release reads.</param>
/// <param name="Fingerprint">The fingerprint of those products and the settings.</param>
public sealed record PreparationContext(
    CatalogueDesignRecord Design,
    JsonElement Snapshot,
    CatalogueDefinition Definition,
    IReadOnlyList<ProductRecord> Rows,
    IReadOnlyDictionary<string, ProductDocument> Products,
    IReadOnlyList<string> References,
    string Fingerprint);
