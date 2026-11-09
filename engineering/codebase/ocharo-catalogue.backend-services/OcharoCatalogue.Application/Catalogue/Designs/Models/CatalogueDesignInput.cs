using System.Text.Json;

namespace OcharoCatalogue.Application.Catalogue.Designs.Models;

/// <summary>Represents the editable fields of a catalogue design: its title and its portable snapshot.</summary>
public sealed record CatalogueDesignInput(
    string Title,
    JsonElement Snapshot,
    int? ExpectedRevision = null);
