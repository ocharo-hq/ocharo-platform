using System.Text;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Application.Preparation.Models;

namespace BrandWorkspace.Infrastructure.DesignDocuments.Services;

/// <summary>Bounds editor JSON without interpreting values as files, URLs, or executable content.</summary>
public sealed class DesignDocumentValidationService
{
    public const int MaximumSnapshotBytes = 1024 * 1024;
    public const int MaximumDepth = 32;
    public const int MaximumNodes = 20000;
    public const int MaximumCollectionItems = 2000;
    public const int MaximumStringLength = 65536;

    public string ValidateKind(string? kind)
    {
        if (kind is not ("catalogue" or "business-card"))
            throw Invalid("Choose catalogue or business-card as the document kind.");
        return kind;
    }

    public DesignDocumentInput ValidateInput(DesignDocumentInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 160
            || input.Title.Any(char.IsControl))
            throw Invalid("Title must contain 1 to 160 characters without control characters.");
        if (input.Snapshot.ValueKind != JsonValueKind.Object)
            throw Invalid("Snapshot must be a JSON object.");
        if (Encoding.UTF8.GetByteCount(input.Snapshot.GetRawText()) > MaximumSnapshotBytes)
            throw new PreparationException("too-large", "Snapshot must contain at most 1 MiB of JSON.");
        var nodes = 0;
        try
        {
            ValidateValue(input.Snapshot, 1, ref nodes);
        }
        catch (InvalidOperationException)
        {
            throw Invalid("Snapshot contains an invalid Unicode value.");
        }
        if (JsonSerializer.SerializeToUtf8Bytes(input.Snapshot, StoredJsonConstants.Default).Length > MaximumSnapshotBytes)
            throw new PreparationException("too-large", "Snapshot must contain at most 1 MiB of stored JSON.");
        return new DesignDocumentInput(input.Title.Trim(), input.Snapshot.Clone());
    }

    public void ValidateRevision(int revision)
    {
        if (revision is < 1 or int.MaxValue)
            throw Invalid("Provide a valid expected revision.");
    }

    public void ValidatePage(int limit, int offset)
    {
        if (limit is < 1 or > 100 || offset is < 0 or > 1000000)
            throw Invalid("Limit must be 1 to 100 and offset must be 0 to 1000000.");
    }

    private static void ValidateValue(JsonElement value, int depth, ref int nodes)
    {
        if (depth > MaximumDepth || ++nodes > MaximumNodes)
            throw Invalid("Snapshot exceeds its nesting or node limit.");
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    if (names.Count >= MaximumCollectionItems || !names.Add(property.Name)
                        || property.Name.Length is 0 or > 256 || property.Name.Any(char.IsControl)
                        || property.Name is "__proto__" or "prototype" or "constructor")
                        throw Invalid("Snapshot contains duplicate, invalid, or too many object properties.");
                    ValidateValue(property.Value, depth + 1, ref nodes);
                }
                break;
            case JsonValueKind.Array:
                if (value.GetArrayLength() > MaximumCollectionItems)
                    throw Invalid("Snapshot arrays must contain at most 2000 items.");
                foreach (JsonElement item in value.EnumerateArray())
                    ValidateValue(item, depth + 1, ref nodes);
                break;
            case JsonValueKind.String:
                string text = value.GetString()!;
                if (text.Length > MaximumStringLength || text.Contains('\0'))
                    throw Invalid("Snapshot strings must contain at most 65536 characters and no null characters.");
                break;
            case JsonValueKind.Number:
                // Keep numeric values interoperable between the browser, .NET, and durable JSON snapshots.
                if (value.GetRawText().Length > 64 || !value.TryGetDouble(out double number)
                    || !double.IsFinite(number) || Math.Abs(number) > 1e12
                    || !value.TryGetDecimal(out _))
                    throw Invalid("Snapshot numbers must be finite decimals between -1000000000000 and 1000000000000.");
                break;
        }
    }

    private static PreparationException Invalid(string message)
    {
        return new PreparationException("validation", message);
    }
}
