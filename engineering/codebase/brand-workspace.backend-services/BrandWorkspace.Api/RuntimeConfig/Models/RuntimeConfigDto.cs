using System.Text.Json.Serialization;

namespace BrandWorkspace.Api.RuntimeConfig.Models;

/// <summary>
/// Represents the public settings the SPA reads once at startup, under its own config names; an address left out is
/// absent.
/// </summary>
public sealed record RuntimeConfigDto
{
    /// <summary>Gets the Desk address.</summary>
    [JsonPropertyName("OCHARO_DESK_URL")]
    public string? Desk { get; init; }

    /// <summary>Gets the Catalogue address.</summary>
    [JsonPropertyName("OCHARO_CATALOGUE_URL")]
    public string? Catalogue { get; init; }

    /// <summary>Gets the Studio address.</summary>
    [JsonPropertyName("OCHARO_STUDIO_URL")]
    public string? Studio { get; init; }

    /// <summary>Gets the Marketing address.</summary>
    [JsonPropertyName("OCHARO_MARKETING_URL")]
    public string? Marketing { get; init; }

    /// <summary>Gets the Editorial address.</summary>
    [JsonPropertyName("OCHARO_EDITORIAL_URL")]
    public string? Editorial { get; init; }

    /// <summary>Gets the Brand address.</summary>
    [JsonPropertyName("OCHARO_BRAND_URL")]
    public string? Brand { get; init; }

    /// <summary>Gets the Atlas address.</summary>
    [JsonPropertyName("OCHARO_ATLAS_URL")]
    public string? Atlas { get; init; }

    /// <summary>Gets the Wear address.</summary>
    [JsonPropertyName("OCHARO_WEAR_URL")]
    public string? Wear { get; init; }

    /// <summary>Gets the Blog address.</summary>
    [JsonPropertyName("OCHARO_BLOG_URL")]
    public string? Blog { get; init; }
}
