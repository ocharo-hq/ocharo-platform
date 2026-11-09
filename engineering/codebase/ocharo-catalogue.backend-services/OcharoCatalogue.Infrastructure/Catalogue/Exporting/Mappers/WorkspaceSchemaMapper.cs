using System.Globalization;
using System.Text.Json.Nodes;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Exporting.Mappers;

/// <summary>Maps a product back to the workspace JSON schema (schema 1.3); unknown source fields stay null rather than
/// invented.</summary>
public static class WorkspaceSchemaMapper
{
    /// <summary>Maps <paramref name="document"/> to its workspace record.</summary>
    /// <param name="document">The product document.</param>
    /// <param name="updatedAt">When the product was last saved.</param>
    public static JsonObject ToWorkspaceSchema(ProductDocument document, DateTimeOffset updatedAt)
    {
        return new JsonObject
        {
            ["schema_version"] = "1.3",
            ["updated_at"] = updatedAt.ToString("O", CultureInfo.InvariantCulture),
            ["id"] = document.Id,
            ["brand"] = "OCHARO",
            ["source"] = new JsonObject
            {
                ["language"] = document.SourceVerbatim is null ? null : "uz",
                ["asset"] = document.Source?.Path,
                ["kind"] = document.SourceVerbatim is null ? "media_only" : "text",
                ["verbatim"] = document.SourceVerbatim,
            },
            ["locales"] = new JsonObject
            {
                ["uz"] = Locale(document.Locales.Uz),
                ["ru"] = Locale(document.Locales.Ru),
                ["en"] = Locale(document.Locales.En),
            },
            ["sizes"] = document.Sizes.Count == 0 ? null : Array(document.Sizes),
            ["classification"] = new JsonObject
            {
                ["status"] = "confirmed",
                ["design_id"] = document.DesignId,
                ["variant_id"] = document.VariantCode,
                ["garment_type"] = document.GarmentType == "look" ? null : document.GarmentType,
                ["fabric_codes"] = Array(document.FabricCodes),
                ["decoration_codes"] = Array(document.DecorationCodes),
            },
            ["assets"] = new JsonObject
            {
                ["source_images"] = Paths(document, MediaRole.Photo),
                ["model_images"] = Paths(document, MediaRole.Model),
                ["cutout_images"] = Paths(document, MediaRole.Cutout),
                ["product_reference_images"] = Paths(document, MediaRole.Reference),
                ["baseline_id"] = null,
            },
            ["review"] = new JsonObject
            {
                ["status"] = document.Issues.Any(issue => issue.Status == IssueStatus.Open) ? "TBD" : "resolved",
                ["issues"] = new JsonArray(document.Issues.Select(Issue).ToArray()),
            },
            ["pricing"] = Pricing(document.Pricing),
        };
    }

    private static JsonObject Locale(ProductLocale locale)
    {
        return new JsonObject
        {
            ["status"] = locale.Status == LocaleStatus.Approved ? "approved" : "draft",
            ["name"] = locale.Name,
            ["fabric"] = locale.Fabric,
            ["decoration"] = locale.Decoration,
            ["description"] = locale.Description,
        };
    }

    private static JsonArray Paths(ProductDocument document, string role)
    {
        var paths = document.Media
            .Where(media => media.Role == role)
            .Select(media => (JsonNode?)(media.SourcePath ?? media.FileName))
            .ToArray();
        return new JsonArray(paths);
    }

    private static JsonArray Array(IReadOnlyList<string> values)
    {
        return new JsonArray(values.Select(value => (JsonNode?)value).ToArray());
    }

    private static JsonNode? Issue(ReviewIssue issue)
    {
        return new JsonObject
        {
            ["field"] = issue.Field,
            ["status"] = issue.Status == IssueStatus.Open ? "TBD" : "resolved",
            ["question"] = issue.Question,
        };
    }

    private static JsonObject? Pricing(ProductPricing? pricing)
    {
        if (pricing is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["currency"] = pricing.Currency,
            ["basis"] = "per_piece",
            ["status"] = pricing.Status == PricingStatus.Confirmed ? "user_confirmed" : "draft",
            ["tiers"] = new JsonArray(pricing.Tiers.Select(Tier).ToArray()),
            ["wording_proposal"] = pricing.Wording,
        };
    }

    private static JsonNode? Tier(PriceTier tier)
    {
        return new JsonObject
        {
            ["minimum_quantity"] = tier.MinimumQuantity,
            ["maximum_quantity"] = tier.MaximumQuantity,
            ["unit_price"] = tier.UnitPrice,
        };
    }
}
