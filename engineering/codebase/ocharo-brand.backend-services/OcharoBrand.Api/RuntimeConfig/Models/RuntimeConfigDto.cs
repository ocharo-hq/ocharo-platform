using System.Text.Json.Serialization;

namespace OcharoBrand.Api.RuntimeConfig.Models;

/// <summary>Represents the public runtime settings, keyed by the config names the Brand SPA declares.</summary>
/// <remarks>Each key is pinned, since the wire contract would camel-case it; an address left out is omitted, never null.</remarks>
public sealed record RuntimeConfigDto
{
    [JsonPropertyName("OCHARO_DESK_URL")]
    public string? DeskUrl { get; init; }

    [JsonPropertyName("OCHARO_CATALOGUE_URL")]
    public string? CatalogueUrl { get; init; }

    [JsonPropertyName("OCHARO_STUDIO_URL")]
    public string? StudioUrl { get; init; }

    [JsonPropertyName("OCHARO_MARKETING_URL")]
    public string? MarketingUrl { get; init; }

    [JsonPropertyName("OCHARO_EDITORIAL_URL")]
    public string? EditorialUrl { get; init; }

    [JsonPropertyName("OCHARO_BRAND_URL")]
    public string? BrandUrl { get; init; }

    [JsonPropertyName("OCHARO_ATLAS_URL")]
    public string? AtlasUrl { get; init; }

    [JsonPropertyName("OCHARO_WEAR_URL")]
    public string? WearUrl { get; init; }

    [JsonPropertyName("OCHARO_BLOG_URL")]
    public string? BlogUrl { get; init; }
}
