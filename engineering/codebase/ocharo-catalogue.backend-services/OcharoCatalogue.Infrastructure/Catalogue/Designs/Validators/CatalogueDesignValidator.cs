using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.Validators;

/// <summary>Validates portable catalogue design snapshots without resolving remote URLs or file paths; a snapshot that
/// breaks a rule fails as invalid through the mediator's exception bridge.</summary>
public static partial class CatalogueDesignValidator
{
    /// <summary>Holds the shared copy fields a design's text may bind to.</summary>
    public static readonly string[] CopyFields =
    [
        "collection",
        "coverNote",
        "header",
        "footer",
        "sizes",
        "pieces",
        "priceOnRequest",
        "prices",
        "materials",
        "decoration",
    ];

    /// <summary>Holds the price display flags of a design's web output.</summary>
    public static readonly string[] PriceFields =
    [
        "show",
        "showCurrency",
        "showQuantity",
        "showRetail",
        "showWholesale",
    ];

    /// <summary>Holds the image groups a design's web output may show.</summary>
    public static readonly string[] MediaGroups = ["all", "model", "product", "detail", "fabric"];

    private static readonly string[] ProductFields =
    [
        "name",
        "fabric",
        "decoration",
        "description",
        "details",
        "notes",
        "sizes",
        "price",
        "price-label",
        "currency",
    ];

    private static readonly string[] Reserved =
    [
        "api",
        "admin",
        "edit",
        "new",
        "pages",
        "editions",
        "collections",
        "products",
        "catalogue",
        "catalogues",
        "assets",
        "media",
        "download",
        "downloads",
        "settings",
        "login",
        "logout",
    ];

    private static readonly string[] ElementColors = ["color", "background", "borderColor"];

    private static readonly (string Key, double Min, double Max)[] ElementNumbers =
    [
        ("fontSize", 0.5, 200.0),
        ("lineHeight", 0.5, 4.0),
        ("letterSpacing", -5.0, 30.0),
        ("cropX", 0.0, 100.0),
        ("cropY", 0.0, 100.0),
        ("zoom", 1.0, 5.0),
        ("opacity", 0.0, 1.0),
        ("rotation", -360.0, 360.0),
        ("radius", 0.0, 1000.0),
        ("borderWidth", 0.0, 20.0),
    ];

    private static readonly string[] TemplateExtensions = [".png", ".jpg", ".jpeg", ".webp", ".svg"];

    /// <summary>Gets the catalogue languages a design may use.</summary>
    public static IReadOnlyList<string> Languages
    {
        get
        {
            return CatalogueLocales.All;
        }
    }

    /// <summary>Tells whether <paramref name="value"/> is an available lowercase catalogue slug.</summary>
    /// <param name="value">The slug.</param>
    public static bool ValidSlug(string value)
    {
        return value.Length is > 0 and <= 100 && SlugPattern().IsMatch(value) && !Reserved.Contains(value);
    }

    /// <summary>Tells whether <paramref name="source"/> names an image bundled with the original template.</summary>
    /// <param name="source">The image source.</param>
    public static bool SafeTemplate(string source)
    {
        return source.StartsWith("/templates/catalogue-original/assets/", StringComparison.Ordinal)
            && !source.Split('/').Any(part => part is "." or "..")
            && source.All(character => char.IsAsciiLetterOrDigit(character) || character is '/' or '-' or '_' or '.')
            && TemplateExtensions.Any(extension => source.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Gets the SHA-256 an uploaded-media source points at; null for any other source.</summary>
    /// <param name="source">The image source.</param>
    public static string? ImageHash(string source)
    {
        var match = MediaPattern().Match(source);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Refuses the request with <paramref name="message"/> unless <paramref name="valid"/>.</summary>
    /// <param name="valid">Whether the rule holds.</param>
    /// <param name="message">The rule, as the person reads it.</param>
    public static void Require(bool valid, string message)
    {
        if (!valid)
        {
            throw CatalogueErrors.Invalid(message).ToException();
        }
    }

    /// <summary>Gets the text property <paramref name="key"/> of <paramref name="value"/>, checked for length and
    /// control characters.</summary>
    /// <param name="value">The JSON object.</param>
    /// <param name="key">The property name.</param>
    /// <param name="max">The longest text accepted.</param>
    /// <param name="empty">Whether blank text and line breaks are accepted.</param>
    public static string Text(
        JsonElement value,
        string key,
        int max = 160,
        bool empty = false)
    {
        Require(
            value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty(key, out var property)
            && property.ValueKind == JsonValueKind.String,
            $"Catalogue {key} must be text.");
        var result = value.GetProperty(key).GetString()!;
        Require(
            result.Length <= max
            && (empty || !string.IsNullOrWhiteSpace(result))
            && !result.Any(character => character == '\0' || (!empty && char.IsControl(character))),
            $"Catalogue {key} is invalid.");
        return result;
    }

    /// <summary>
    /// Gets the number property <paramref name="key"/> of <paramref name="value"/>, checked against a range.
    /// </summary>
    /// <param name="value">The JSON object.</param>
    /// <param name="key">The property name.</param>
    /// <param name="min">The smallest number accepted.</param>
    /// <param name="max">The largest number accepted.</param>
    public static double Number(
        JsonElement value,
        string key,
        double min,
        double max)
    {
        Require(
            value.TryGetProperty(key, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetDouble(out var number)
            && double.IsFinite(number)
            && number >= min
            && number <= max,
            $"Catalogue {key} is invalid.");
        return value.GetProperty(key).GetDouble();
    }

    /// <summary>Gets the boolean property <paramref name="key"/> of <paramref name="value"/>.</summary>
    /// <param name="value">The JSON object.</param>
    /// <param name="key">The property name.</param>
    public static bool Flag(JsonElement value, string key)
    {
        if (value.TryGetProperty(key, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return property.GetBoolean();
        }

        throw CatalogueErrors.Invalid($"Catalogue {key} must be a boolean.").ToException();
    }

    /// <summary>Tells whether a page is active; a page without the flag is.</summary>
    /// <param name="page">The page.</param>
    public static bool Active(JsonElement page)
    {
        return !page.TryGetProperty("isActive", out var active) || active.ValueKind == JsonValueKind.True;
    }

    /// <summary>Gets the validated catalogue metadata of <paramref name="snapshot"/>.</summary>
    /// <param name="snapshot">The design snapshot.</param>
    public static CatalogueDefinition Definition(JsonElement snapshot)
    {
        Require(
            snapshot.TryGetProperty("catalogue", out var catalogue) && catalogue.ValueKind == JsonValueKind.Object,
            "Catalogue metadata is required.");
        var slug = Text(
            catalogue,
            "slug",
            100);
        Require(ValidSlug(slug), "Choose an available lowercase catalogue slug.");
        var description = Text(
            catalogue,
            "description",
            2000,
            true);
        var active = Flag(catalogue, "isActive");
        Require(
            catalogue.TryGetProperty("outputs", out var output) && output.ValueKind == JsonValueKind.Object,
            "Choose catalogue outputs.");
        var outputs = new CatalogueOutputs(Flag(output, "web"), Flag(output, "pdf"));
        Require(outputs.Web || outputs.Pdf, "Enable a web or PDF output.");
        Require(
            catalogue.TryGetProperty("web", out var web) && web.ValueKind == JsonValueKind.Object,
            "Web settings are required.");
        var theme = Text(
            web,
            "theme",
            20);
        Require(theme is "editorial" or "grid", "Choose a supported web theme.");
        Require(
            web.TryGetProperty("pages", out var pages)
            && pages.ValueKind == JsonValueKind.Array
            && pages.GetArrayLength() <= 100,
            "Web pages must be an array of up to 100 items.");
        var result = new List<CatalogueWebPage>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages.EnumerateArray())
        {
            var id = Text(
                page,
                "id",
                100);
            Require(SafeId(id) && ids.Add(id), "Page IDs must be safe and distinct.");
            var title = Text(page, "title");
            var layout = Text(
                page,
                "layout",
                20);
            Require(layout is "spread" or "grid" or "collage", "Choose a supported web layout.");
            var products = ProductArray(
                page,
                "productIds",
                1000);
            result.Add(new CatalogueWebPage(
                id,
                title,
                layout,
                Flag(page, "isActive"),
                products));
        }

        return new CatalogueDefinition(
            slug,
            description,
            active,
            outputs,
            theme,
            result);
    }

    /// <summary>Validates a design's input and returns its snapshot, detached from the request.</summary>
    /// <param name="input">The design fields.</param>
    public static JsonElement Validate(CatalogueDesignInput input)
    {
        Require(
            !string.IsNullOrWhiteSpace(input.Title) && input.Title.Length <= 160 && !input.Title.Any(char.IsControl),
            "Title must contain 1 to 160 characters.");
        var snapshot = input.Snapshot;
        Require(snapshot.ValueKind == JsonValueKind.Object, "Snapshot must be an object.");
        Require(Encoding.UTF8.GetByteCount(snapshot.GetRawText()) <= 1024 * 1024, "Snapshot exceeds 1 MiB.");
        var nodes = 0;
        ValidateTree(
            snapshot,
            0,
            ref nodes);
        var version = Number(
            snapshot,
            "schemaVersion",
            1,
            1);
        Require(version == 1 && Text(snapshot, "kind") == "catalogue", "Choose a version 1 catalogue snapshot.");
        var locale = Text(
            snapshot,
            "locale",
            20);
        Require(Languages.Contains(locale), "Choose a supported catalogue language.");
        Text(
            snapshot,
            "templateId",
            120);
        _ = Definition(snapshot);
        Require(
            snapshot.TryGetProperty("pages", out var pages)
            && pages.ValueKind == JsonValueKind.Array
            && pages.GetArrayLength() <= 100,
            "PDF pages must be an array of up to 100 items.");
        var ids = new HashSet<string>();
        foreach (var page in pages.EnumerateArray())
        {
            ValidatePage(page, ids);
        }

        if (snapshot.TryGetProperty("catalogueSettings", out var settings))
        {
            ValidateSettings(settings);
        }

        if (snapshot.TryGetProperty("productRevisions", out var revisions))
        {
            Require(
                revisions.ValueKind == JsonValueKind.Object && revisions.EnumerateObject().Count() <= 1000,
                "Product revision evidence must be an object.");
            foreach (var entry in revisions.EnumerateObject())
            {
                Require(
                    SlugPattern().IsMatch(entry.Name)
                    && entry.Value.ValueKind == JsonValueKind.Number
                    && entry.Value.TryGetInt32(out var revision)
                    && revision > 0,
                    "Invalid product revision evidence.");
            }
        }

        _ = References(snapshot);
        return snapshot.Clone();
    }

    /// <summary>Gets every product a snapshot references, from its web pages and its PDF pages.</summary>
    /// <param name="snapshot">The design snapshot.</param>
    public static IReadOnlyList<string> References(JsonElement snapshot)
    {
        return Definition(snapshot).WebPages
            .SelectMany(page => page.ProductIds)
            .Concat(snapshot.GetProperty("pages").EnumerateArray().SelectMany(ReferencesOnPage))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>Gets the products one PDF page references explicitly: its list, its source and its elements.</summary>
    /// <param name="page">The PDF page.</param>
    public static IReadOnlyList<string> ReferencesOnPage(JsonElement page)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (page.TryGetProperty("productIds", out _))
        {
            foreach (var id in ProductArray(
                page,
                "productIds",
                300))
            {
                ids.Add(id);
            }
        }

        if (page.TryGetProperty("catalogueSource", out var source))
        {
            Require(source.ValueKind == JsonValueKind.Object, "Invalid catalogue source.");
            AddReference(ids, source);
        }

        foreach (var element in page.GetProperty("elements").EnumerateArray())
        {
            AddReference(ids, element);
            if (element.TryGetProperty("binding", out var binding))
            {
                AddReference(ids, binding);
            }
        }

        return ids.ToArray();
    }

    /// <summary>Gets every uploaded image a snapshot's PDF pages show.</summary>
    /// <param name="snapshot">The design snapshot.</param>
    public static IReadOnlyList<string> ImageHashes(JsonElement snapshot)
    {
        return snapshot.GetProperty("pages").EnumerateArray()
            .SelectMany(page => page.GetProperty("elements").EnumerateArray())
            .Where(element => element.TryGetProperty("src", out _))
            .Select(element => ImageHash(element.GetProperty("src").GetString()!))
            .OfType<string>()
            .Distinct()
            .ToArray();
    }

    private static void ValidatePage(JsonElement page, HashSet<string> ids)
    {
        var id = Text(
            page,
            "id",
            100);
        Require(SafeId(id) && ids.Add(id), "PDF page IDs must be safe and distinct.");
        Text(page, "title");
        Number(
            page,
            "width",
            10,
            1000);
        Number(
            page,
            "height",
            10,
            1000);
        Color(page, "background");
        if (page.TryGetProperty("isActive", out _))
        {
            Flag(page, "isActive");
        }

        if (page.TryGetProperty("productIds", out _))
        {
            ProductArray(
                page,
                "productIds",
                300);
        }

        Require(
            page.TryGetProperty("elements", out var elements)
            && elements.ValueKind == JsonValueKind.Array
            && elements.GetArrayLength() <= 300,
            "PDF elements must be an array of up to 300 items.");
        var elementIds = new HashSet<string>();
        foreach (var element in elements.EnumerateArray())
        {
            ValidateElement(element, elementIds);
        }
    }

    private static void ValidateElement(JsonElement element, HashSet<string> elementIds)
    {
        var elementId = Text(
            element,
            "id",
            256);
        Require(elementIds.Add(elementId), "Element IDs must be distinct.");
        Text(
            element,
            "label",
            256);
        var type = Text(
            element,
            "type",
            20);
        Require(type is "text" or "image" or "shape", "Choose a supported element type.");
        Number(
            element,
            "x",
            -1000,
            1000);
        Number(
            element,
            "y",
            -1000,
            1000);
        Number(
            element,
            "width",
            0.1,
            2000);
        Number(
            element,
            "height",
            0.1,
            2000);
        if (element.TryGetProperty("src", out _))
        {
            var source = Text(
                element,
                "src",
                300);
            Require(
                ImageHash(source) is not null || SafeTemplate(source),
                "Images must use uploaded catalogue media or the bundled original template.");
        }

        foreach (var key in ElementColors)
        {
            if (element.TryGetProperty(key, out _))
            {
                Color(element, key);
            }
        }

        if (element.TryGetProperty("text", out _))
        {
            Text(
                element,
                "text",
                10000,
                true);
        }

        if (element.TryGetProperty("direction", out _))
        {
            Require(Text(element, "direction") is "ltr" or "rtl", "Invalid text direction.");
        }

        foreach (var (key, min, max) in ElementNumbers)
        {
            if (element.TryGetProperty(key, out _))
            {
                Number(
                    element,
                    key,
                    min,
                    max);
            }
        }

        if (element.TryGetProperty("binding", out var binding))
        {
            ValidateBinding(
                element,
                binding,
                type);
        }
    }

    private static void ValidateBinding(
        JsonElement element,
        JsonElement binding,
        string type)
    {
        Require(type == "text" && binding.ValueKind == JsonValueKind.Object, "Only text supports field bindings.");
        var kind = Text(
            binding,
            "kind",
            20);
        var field = Text(
            binding,
            "field",
            30);
        var known = kind == "shared" ? CopyFields.Contains(field) : kind == "product" && ProductFields.Contains(field);
        Require(known, "Unknown catalogue binding.");
        if (kind == "product")
        {
            Require(
                binding.TryGetProperty("productId", out _) || element.TryGetProperty("productId", out _),
                "Product bindings need a product.");
        }

        if (binding.TryGetProperty("index", out _) || field is "price" or "price-label")
        {
            var index = Number(
                binding,
                "index",
                0,
                49);
            Require(index % 1 == 0, "Price index must be an integer.");
        }
    }

    private static void ValidateSettings(JsonElement settings)
    {
        Require(settings.ValueKind == JsonValueKind.Object, "Catalogue settings must be an object.");
        var group = Text(
            settings,
            "mediaGroup",
            20);
        Require(MediaGroups.Contains(group), "Choose a supported image group.");
        Require(
            settings.TryGetProperty("priceDisplay", out var display) && display.ValueKind == JsonValueKind.Object,
            "Price display settings are required.");
        foreach (var key in PriceFields)
        {
            Flag(display, key);
        }

        Require(
            settings.TryGetProperty("copyByLocale", out var copies)
            && copies.ValueKind == JsonValueKind.Object
            && copies.EnumerateObject().Count() <= Languages.Count,
            "Catalogue copy must contain supported languages.");
        foreach (var item in copies.EnumerateObject())
        {
            Require(Languages.Contains(item.Name), "Unsupported copy language.");
            foreach (var key in CopyFields)
            {
                Text(
                    item.Value,
                    key,
                    1000,
                    true);
            }
        }
    }

    private static void AddReference(HashSet<string> ids, JsonElement value)
    {
        if (!value.TryGetProperty("productId", out _))
        {
            return;
        }

        var id = Text(
            value,
            "productId",
            120);
        Require(SlugPattern().IsMatch(id), "Invalid product reference.");
        ids.Add(id);
    }

    private static IReadOnlyList<string> ProductArray(
        JsonElement value,
        string key,
        int max)
    {
        Require(
            value.TryGetProperty(key, out var array)
            && array.ValueKind == JsonValueKind.Array
            && array.GetArrayLength() <= max,
            "Product IDs must be an array.");
        var result = array.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()! : "")
            .ToArray();
        Require(
            result.All(id => id.Length is > 0 and <= 120 && SlugPattern().IsMatch(id))
            && result.Distinct().Count() == result.Length,
            "Product IDs must be distinct valid slugs.");
        return result;
    }

    private static bool SafeId(string value)
    {
        return value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }

    private static void Color(JsonElement value, string key)
    {
        var color = Text(
            value,
            key,
            7);
        Require(ColorPattern().IsMatch(color), "Colors must use six-digit hex values.");
    }

    private static void ValidateTree(
        JsonElement value,
        int depth,
        ref int nodes)
    {
        Require(depth <= 32 && ++nodes <= 20000, "Snapshot exceeds its depth or node limit.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>();
            foreach (var item in value.EnumerateObject())
            {
                Require(
                    names.Add(item.Name)
                    && names.Count <= 2000
                    && item.Name.Length is > 0 and <= 256
                    && !item.Name.Any(char.IsControl)
                    && item.Name is not ("__proto__" or "prototype" or "constructor"),
                    "Invalid snapshot keys.");
                ValidateTree(
                    item.Value,
                    depth + 1,
                    ref nodes);
            }
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            Require(value.GetArrayLength() <= 2000, "Snapshot arrays exceed 2000 items.");
            foreach (var item in value.EnumerateArray())
            {
                ValidateTree(
                    item,
                    depth + 1,
                    ref nodes);
            }
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            Require(text.Length <= 65536 && !text.Contains('\0'), "Invalid snapshot text.");
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            Require(
                value.TryGetDouble(out var number) && double.IsFinite(number) && Math.Abs(number) <= 1e12,
                "Invalid snapshot number.");
        }
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^/api/catalogue/media/([a-f0-9]{64})(?:/preview)?$")]
    private static partial Regex MediaPattern();

    [GeneratedRegex("^#[a-fA-F0-9]{6}$")]
    private static partial Regex ColorPattern();
}
