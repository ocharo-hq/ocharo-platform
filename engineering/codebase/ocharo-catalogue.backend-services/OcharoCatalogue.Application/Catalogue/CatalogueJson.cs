using System.Text.Json;
using System.Text.Json.Serialization;

namespace OcharoCatalogue.Application.Catalogue;

/// <summary>One serializer configuration for stored documents and exports.</summary>
public static class CatalogueJson
{
    /// <summary>Camel-case, null-tolerant options shared by persistence and export.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    /// <summary>Indented variant for human-readable exports.</summary>
    public static readonly JsonSerializerOptions Indented = new(Options) { WriteIndented = true };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Stored {typeof(T).Name} is empty.");
}
