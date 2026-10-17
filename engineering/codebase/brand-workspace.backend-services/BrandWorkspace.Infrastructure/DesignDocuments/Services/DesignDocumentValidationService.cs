using System.Text;
using BrandWorkspace.Application.Commerce;
using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;
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
            ValidateCatalogueMetadata(input.Snapshot);
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

    private static readonly string[] CopyFields = ["collection", "coverNote", "header", "footer", "sizes", "pieces", "priceOnRequest", "prices", "materials", "decoration"];
    private static readonly string[] ProductFields = ["name", "fabric", "decoration", "description", "details", "notes", "sizes", "price", "price-label", "currency"];
    private static readonly string[] PriceFields = ["show", "showCurrency", "showQuantity", "showRetail", "showWholesale"];

    private static bool String(JsonElement value, out string text)
    {
        text = value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        return value.ValueKind == JsonValueKind.String;
    }

    private static bool Group(JsonElement value) => String(value, out var group) && group is "all" or "model" or "product" or "detail" or "fabric";

    private static void ValidateCatalogueMetadata(JsonElement snapshot)
    {
        var catalogue = snapshot.TryGetProperty("kind", out var kind) && String(kind, out var name) && name == "catalogue";
        if (catalogue && snapshot.TryGetProperty("locale", out var locale) && (!String(locale, out var code) || !CatalogueLanguages.Supported(code)))
            throw Invalid("Choose a supported catalogue language.");
        if (snapshot.TryGetProperty("catalogue", out _))
        {
            if (!catalogue) throw Invalid("Catalogue metadata belongs to catalogue documents.");
            _ = CatalogueDefinition.Read(snapshot);
            if (!snapshot.TryGetProperty("pages", out var outputPages) || outputPages.ValueKind != JsonValueKind.Array || outputPages.GetArrayLength() > 100)
                throw Invalid("Catalogue PDF pages must be an array of up to 100 pages.");
        }
        if (snapshot.TryGetProperty("catalogueSettings", out var settings))
        {
            if (!catalogue || settings.ValueKind != JsonValueKind.Object)
                throw Invalid("Catalogue settings belong to catalogue documents.");
            if (settings.TryGetProperty("brandId", out var brand) && (!String(brand, out var brandId) || !Guid.TryParse(brandId, out var id) || id == Guid.Empty))
                throw Invalid("Catalogue settings require a valid brand ID.");
            if (settings.TryGetProperty("sourceRevision", out var revision) && (revision.ValueKind != JsonValueKind.Number || !revision.TryGetInt32(out var number) || number < 1))
                throw Invalid("Catalogue source revision must be positive.");
            if (!settings.TryGetProperty("mediaGroup", out var group) || !Group(group))
                throw Invalid("Choose a known catalogue image group.");
            if (!settings.TryGetProperty("priceDisplay", out var prices) || prices.ValueKind != JsonValueKind.Object
                || PriceFields.Any(field => !prices.TryGetProperty(field, out var flag) || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
                throw Invalid("Catalogue price display settings must contain five boolean flags.");
            if (!settings.TryGetProperty("copyByLocale", out var copies) || copies.ValueKind != JsonValueKind.Object
                || copies.EnumerateObject().Count() > CatalogueLanguages.Codes.Count)
                throw Invalid("Catalogue shared copy must use supported language records.");
            foreach (var copy in copies.EnumerateObject())
            {
                if (!CatalogueLanguages.Supported(copy.Name) || copy.Value.ValueKind != JsonValueKind.Object
                    || copy.Value.EnumerateObject().Any(field => !CopyFields.Contains(field.Name))
                    || CopyFields.Any(field => !copy.Value.TryGetProperty(field, out var text) || !String(text, out var content) || content.Length > 1000))
                    throw Invalid("Shared catalogue copy requires ten text fields of up to 1000 characters per language.");
            }
        }
        if (!snapshot.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array) return;
        foreach (var page in pages.EnumerateArray())
        {
            if (page.ValueKind != JsonValueKind.Object) continue;
            if (page.TryGetProperty("isActive", out var active) && active.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid("Page activity must be a boolean.");
            if (page.TryGetProperty("catalogueSource", out var source) && source.ValueKind == JsonValueKind.Object
                && source.TryGetProperty("mediaGroup", out var selected) && !Group(selected))
                throw Invalid("Choose a known page image group.");
            if (!page.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array) continue;
            foreach (var element in elements.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                if (element.TryGetProperty("direction", out var direction)
                    && (!String(direction, out var flow) || flow is not ("ltr" or "rtl")))
                    throw Invalid("Text direction must be ltr or rtl.");
                if (!element.TryGetProperty("binding", out var binding)) continue;
                if (!catalogue || binding.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("type", out var type) || !String(type, out var elementType) || elementType != "text"
                    || !binding.TryGetProperty("kind", out var bindingKind) || !String(bindingKind, out var bindingType)
                    || !binding.TryGetProperty("field", out var field) || !String(field, out var fieldName))
                    throw Invalid("Catalogue text bindings must identify their source and field.");
                if (bindingType == "shared")
                {
                    if (!CopyFields.Contains(fieldName)) throw Invalid("Unknown shared catalogue field.");
                }
                else if (bindingType == "product")
                {
                    var hasProduct = binding.TryGetProperty("productId", out var product) || element.TryGetProperty("productId", out product);
                    if (!ProductFields.Contains(fieldName) || !hasProduct || !String(product, out var productId)
                        || string.IsNullOrWhiteSpace(productId) || productId.Length > 256)
                        throw Invalid("Product text bindings require a product and supported field.");
                    var hasIndex = binding.TryGetProperty("index", out var index);
                    if ((fieldName is "price" or "price-label") && !hasIndex
                        || hasIndex && (index.ValueKind != JsonValueKind.Number || !index.TryGetInt32(out var position) || position is < 0 or > 49))
                        throw Invalid("Catalogue price bindings require an index from zero to 49.");
                }
                else throw Invalid("Unknown catalogue binding source.");
            }
        }
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
