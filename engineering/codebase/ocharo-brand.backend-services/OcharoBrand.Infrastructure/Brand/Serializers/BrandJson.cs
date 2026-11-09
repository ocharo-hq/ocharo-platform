using System.Text.Json;

namespace OcharoBrand.Infrastructure.Brand.Serializers;

/// <summary>One serializer configuration for stored documents; the stored contract, never the wire's.</summary>
public static class BrandJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Stored {typeof(T).Name} is empty.");
}
