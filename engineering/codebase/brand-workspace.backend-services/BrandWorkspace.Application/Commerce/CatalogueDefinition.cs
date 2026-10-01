using System.Text.Json;
using System.Text.RegularExpressions;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Commerce;

namespace BrandWorkspace.Application.Commerce;

/// <summary>Validates the additive catalogue metadata stored in a single saved design revision.</summary>
public static partial class CatalogueDefinition
{
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal) { "api", "admin", "edit", "new", "pages", "editions", "collections", "products", "catalogue", "catalogues", "assets", "media", "download", "downloads", "settings", "login", "logout" };
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
    public static bool ValidSlug(string? value) => value is { Length: > 0 and <= 100 } && SlugPattern().IsMatch(value) && !Reserved.Contains(value);
    public static bool Active(JsonElement page) => !page.TryGetProperty("isActive", out var active) || active.ValueKind == JsonValueKind.True;
    public static CatalogueDefinitionMetadata? Read(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("catalogue", out var value)) return null;
        Require(value.ValueKind == JsonValueKind.Object, "Catalogue metadata must be an object.");
        var slug = Text(value, "slug", 100, false);
        Require(ValidSlug(slug), "Choose a lowercase catalogue slug with single hyphens; reserved paths are unavailable.");
        var description = Text(value, "description", 2000, true);
        var active = Flag(value, "isActive");
        Require(value.TryGetProperty("outputs", out var outputs) && outputs.ValueKind == JsonValueKind.Object, "Choose catalogue outputs.");
        var webEnabled = Flag(outputs, "web"); var pdfEnabled = Flag(outputs, "pdf");
        Require(webEnabled || pdfEnabled, "Enable a web or PDF catalogue output.");
        Require(value.TryGetProperty("web", out var web) && web.ValueKind == JsonValueKind.Object, "Catalogue web settings are required.");
        var theme = Text(web, "theme", 20, false);
        Require(theme is "editorial" or "grid", "Choose an editorial or grid web theme.");
        Require(web.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array && pages.GetArrayLength() <= 100, "Web catalogues support up to 100 pages.");
        var result = new List<CatalogueWebPage>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages.EnumerateArray())
        {
            Require(page.ValueKind == JsonValueKind.Object, "Web pages must be objects.");
            var id = Text(page, "id", 100, false); var title = Text(page, "title", 160, false); var layout = Text(page, "layout", 20, false);
            Require(id.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') && ids.Add(id), "Web page IDs must be safe and distinct.");
            Require(layout is "spread" or "grid" or "collage", "Choose a supported web page layout.");
            var isActive = Flag(page, "isActive");
            Require(page.TryGetProperty("productIds", out var products) && products.ValueKind == JsonValueKind.Array && products.GetArrayLength() <= 1000, "Web page product references are invalid.");
            var productIds = products.EnumerateArray().Select(product => product.ValueKind == JsonValueKind.String ? product.GetString()! : "").ToArray();
            Require(productIds.All(product => product.Length is > 0 and <= 120 && SlugPattern().IsMatch(product)) && productIds.Distinct().Count() == productIds.Length, "Web page product references must be distinct product IDs.");
            result.Add(new(id, title, layout, productIds) { IsActive = isActive });
        }
        return new(slug, description, active, webEnabled, pdfEnabled, new(theme, result));
    }
    public static IReadOnlyList<string> ProductIds(JsonElement page)
    {
        Require(page.ValueKind == JsonValueKind.Object, "Catalogue pages must be objects.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Add(JsonElement value, string key) { if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var id) && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString())) ids.Add(id.GetString()!); }
        if (page.TryGetProperty("productIds", out var products) && products.ValueKind == JsonValueKind.Array)
            foreach (var product in products.EnumerateArray()) { Require(product.ValueKind == JsonValueKind.String, "Product references must be text."); ids.Add(product.GetString()!); }
        if (page.TryGetProperty("catalogueSource", out var source)) Add(source, "productId");
        if (page.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
            foreach (var element in elements.EnumerateArray()) { Add(element, "productId"); if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("binding", out var binding)) Add(binding, "productId"); }
        return ids.ToArray();
    }
    public static IReadOnlyList<string> ProductIdsForSnapshot(JsonElement snapshot)
    {
        var ids = Read(snapshot)?.Web.Pages.SelectMany(page => page.ProductIds) ?? [];
        if (snapshot.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array) ids = ids.Concat(pages.EnumerateArray().SelectMany(ProductIds));
        return ids.Distinct().ToArray();
    }
    private static string Text(JsonElement value, string key, int max, bool empty)
    {
        Require(value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String, $"Catalogue {key} must be text.");
        var text = field.GetString()!;
        Require(text.Length <= max && (empty || !string.IsNullOrWhiteSpace(text)) && !text.Any(character => character == '\0' || !empty && char.IsControl(character)), $"Catalogue {key} is invalid.");
        return text;
    }
    private static bool Flag(JsonElement value, string key)
    {
        Require(value.TryGetProperty(key, out var field) && field.ValueKind is JsonValueKind.True or JsonValueKind.False, $"Catalogue {key} must be a boolean.");
        return field.GetBoolean();
    }
    private static void Require(bool valid, string message) { if (!valid) throw new PreparationException("validation", message); }
}

public sealed record CatalogueDefinitionMetadata(string Slug, string Description, bool IsActive, bool WebEnabled, bool PdfEnabled, CatalogueWebVersion Web);
