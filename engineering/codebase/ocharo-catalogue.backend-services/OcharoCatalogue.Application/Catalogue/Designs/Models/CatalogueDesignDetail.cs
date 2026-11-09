using System.Text.Json;

namespace OcharoCatalogue.Application.Catalogue.Designs.Models;

/// <summary>Represents a catalogue design with its revision stamp and snapshot.</summary>
public sealed record CatalogueDesignDetail(
    Guid Id,
    string Title,
    int Revision,
    bool Archived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    JsonElement Snapshot);
