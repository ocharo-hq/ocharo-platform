using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace BrandWorkspace.Persistence.Commerce;

/// <summary>Commerce dictionary keys are domain identifiers, including case-sensitive size labels.</summary>
public static class CommerceJson
{
    public static JsonSerializerOptions Options { get; } = new(StoredJsonConstants.Default) { DictionaryKeyPolicy = null };
}
