namespace OcharoCatalogue.Application.Catalogue.Designs.Models;

/// <summary>Represents the validated catalogue metadata of a design snapshot.</summary>
public sealed record CatalogueDefinition(
    string Slug,
    string Description,
    bool IsActive,
    CatalogueOutputs Outputs,
    string Theme,
    IReadOnlyList<CatalogueWebPage> WebPages);
