using System.Text.Json;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Validators;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.Mappers;

/// <summary>Maps stored designs to the views the gallery and the designer read.</summary>
public static class DesignProjections
{
    /// <summary>Maps a stored design to its detail.</summary>
    /// <param name="record">The design row.</param>
    public static CatalogueDesignDetail ToDetail(CatalogueDesignRecord record)
    {
        return new CatalogueDesignDetail(
            record.Id,
            record.Title,
            record.Revision,
            record.Archived,
            record.CreatedAtUtc,
            record.UpdatedAtUtc,
            CatalogueJson.Read<JsonElement>(record.Json));
    }

    /// <summary>Maps a stored design to its gallery card.</summary>
    /// <param name="record">The design row.</param>
    public static CatalogueDesignSummary ToSummary(CatalogueDesignRecord record)
    {
        var snapshot = CatalogueJson.Read<JsonElement>(record.Json);
        var definition = CatalogueDesignValidator.Definition(snapshot);
        var web = new CatalogueWebSummary(definition.Theme, definition.WebPages.Count(page => page.IsActive));
        var pdfPages = snapshot.GetProperty("pages").EnumerateArray().Count(CatalogueDesignValidator.Active);
        var metadata = new CatalogueDesignSummaryMetadata(
            definition.Slug,
            definition.Description,
            definition.IsActive,
            definition.Outputs,
            web,
            pdfPages);
        return new CatalogueDesignSummary(
            record.Id,
            record.Title,
            record.Revision,
            record.Archived,
            record.CreatedAtUtc,
            record.UpdatedAtUtc,
            record.Locale,
            metadata);
    }
}
