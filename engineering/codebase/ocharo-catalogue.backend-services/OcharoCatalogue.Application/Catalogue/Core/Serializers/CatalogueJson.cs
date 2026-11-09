using System.Text.Json;
using System.Text.Json.Serialization;

namespace OcharoCatalogue.Application.Catalogue.Core.Serializers;

/// <summary>Holds the one serializer configuration for stored documents and exports.</summary>
public static class CatalogueJson
{
    /// <summary>Holds the camel-case, null-tolerant options shared by persistence and export.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    /// <summary>Holds the indented variant for human-readable exports.</summary>
    public static readonly JsonSerializerOptions Indented = new(Options) { WriteIndented = true };

    /// <summary>Writes <paramref name="value"/> as a stored document.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="value">The document to write.</param>
    public static string Write<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    /// <summary>Reads a stored document; an empty one is a broken store.</summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="json">The stored text.</param>
    public static T Read<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidOperationException($"Stored {typeof(T).Name} is empty.");
    }
}
