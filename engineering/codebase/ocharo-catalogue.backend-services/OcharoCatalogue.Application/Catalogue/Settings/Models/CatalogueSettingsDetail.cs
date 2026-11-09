using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Settings.Models;

/// <summary>Represents the catalogue settings with their revision and the configured import folder.</summary>
/// <param name="Settings">The settings; the Ocharo defaults until first saved.</param>
/// <param name="Revision">The saved revision; 0 until first saved.</param>
/// <param name="SourceRoot">The workspace products folder imports read from; null when none is configured.</param>
public sealed record CatalogueSettingsDetail(
    CatalogueSettings Settings,
    int Revision,
    string? SourceRoot);
