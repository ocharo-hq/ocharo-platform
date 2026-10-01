using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Commerce;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace BrandWorkspace.Application.Commerce;

/// <summary>Bounds edits and separates private source material from explicitly approved publication.</summary>
public static partial class CommerceRules
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")] private static partial Regex SlugPattern();
    [GeneratedRegex("^[a-f0-9]{64}$")] private static partial Regex HashPattern();
    [GeneratedRegex("^#[a-fA-F0-9]{6}$")] private static partial Regex ColorPattern();
    public static bool Hash(string? value) => value is not null && HashPattern().IsMatch(value);
    public static void Validate(CommerceSnapshot snapshot)
    {
        Require(snapshot is not null && snapshot.Identity is not null, "Brand identity is required.");
        Require(snapshot!.Slug is { Length: > 0 and <= 100 } && SlugPattern().IsMatch(snapshot.Slug), "Use a lowercase public slug.");
        Require(snapshot.Identity.Name is { Length: > 0 and <= 120 }, "Brand name is required (maximum 120 characters).");
        Require(snapshot.Products is not null && snapshot.Collections is not null && snapshot.Logos is not null && snapshot.MediaApprovals is not null && snapshot.VerifiedLinkKeys is not null && snapshot.Sources is not null, "All collections must be present.");
        Require(snapshot.Products.Count <= 1000 && snapshot.Collections.Count <= 200 && snapshot.MediaApprovals.Count <= 5000, "Draft exceeds supported limits.");
        Require(JsonSerializer.Serialize(snapshot).Length <= 4_000_000, "Draft is too large.");
        Require(snapshot.Identity.Palette is not null && snapshot.Identity.Links is not null && snapshot.Identity.Tagline is not null && snapshot.Identity.Descriptor is not null, "Identity fields must be present.");
        Require(snapshot.Identity.Palette.All(c => c is not null && ColorPattern().IsMatch(c.Hex ?? "")), "Colors must use six-digit hexadecimal values.");
        Require(snapshot.Identity.Links.All(l => l is not null && Https(l.Url)), "Brand links must be absolute HTTPS destinations without credentials.");
        Require(snapshot.Identity.Links.Select(l => l.Key).Distinct().Count() == snapshot.Identity.Links.Count, "Brand link keys must be unique.");
        Require(snapshot.VerifiedLinkKeys.All(key => snapshot.Identity.Links.Any(l => l.Key == key)), "Verified links must exist in the identity.");
        Require(snapshot.Products.All(p => p is not null && p.Document is not null), "Products cannot be null.");
        Require(snapshot.Products.Select(p => p.Document.Id).Distinct().Count() == snapshot.Products.Count, "Product IDs must be unique.");
        foreach (var item in snapshot.Products)
        {
            var p = item.Document;
            Require(p.Id is { Length: > 0 and <= 120 } && SlugPattern().IsMatch(p.Id), "Product IDs must be stable lowercase slugs.");
            Require(p.Status is "draft" or "active" or "archived", "Unknown product status.");
            Require(p.Locales?.Uz is not null && p.Locales.Ru is not null && p.Locales.En is not null, "All three locales are required.");
            Require(p.Sizes is not null && p.Media is not null && p.Issues is not null && p.GarmentType is { Length: > 0 and <= 100 } && p.VariantCode is { Length: > 0 and <= 100 }, "Product fields are incomplete.");
            Require(p.Sizes.Count <= 100 && p.Sizes.All(s => !string.IsNullOrWhiteSpace(s) && s.Length <= 40) && p.Sizes.Distinct().Count() == p.Sizes.Count, "Sizes must be distinct labels.");
            foreach (var locale in new[] { p.Locales!.Uz, p.Locales.Ru, p.Locales.En })
                Require(locale.Status is "draft" or "approved" && (locale.Status != "approved" || !string.IsNullOrWhiteSpace(locale.Name)), "Approved copy needs a title.");
            Require(p.Media.All(m => m is not null && Hash(m.Sha256) && m.ContentType is "image/png" or "image/jpeg" or "image/webp"), "Product images need valid hashes and supported image types.");
            Require(p.Stock is null || p.Stock.All(s => p.Sizes.Contains(s.Key) && s.Value >= 0), "Stock must use known sizes and nonnegative counts.");
            if (p.Pricing is { } price)
                Require(price.Status is "draft" or "confirmed" && price.Currency is { Length: 3 } && price.Currency.All(char.IsAsciiLetterUpper)
                    && price.Tiers is not null && price.Tiers.All(t => t.MinimumQuantity > 0 && t.UnitPrice >= 0 && (t.MaximumQuantity is null || t.MaximumQuantity >= t.MinimumQuantity))
                    && price.Tiers.Count(t => t.MinimumQuantity == 1) <= 1, "Pricing must use a currency and valid nonnegative tiers.");
        }
        Require(snapshot.CatalogueEditions is not null && snapshot.CatalogueEditions.Count <= 3 && snapshot.CatalogueEditions.Select(e=>e.Locale).Distinct().Count()==snapshot.CatalogueEditions.Count, "Catalogue editions must have distinct supported languages.");
        foreach(var e in snapshot.CatalogueEditions)
        {
            Require(e.Id!=Guid.Empty && e.SourceDesignId!=Guid.Empty && e.SourceRevision>0 && e.Locale is "uz" or "ru" or "en" && !string.IsNullOrWhiteSpace(e.Title) && e.Title.Length<=160 && e.TemplateId.Length<=120 && Hash(e.PdfSha256), "Catalogue edition metadata is invalid.");
            Require(e.Pages.Count is >0 and <=100 && e.Pages.Select(p=>p.Id).Distinct().Count()==e.Pages.Count, "Catalogue edition pages must be distinct.");
            Require(e.Pages.All(p=>p.Id.Length is >0 and <=100 && p.Id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_') && p.Title.Length<=160 && double.IsFinite(p.WidthMm) && double.IsFinite(p.HeightMm) && p.WidthMm is >0 and <=2000 && p.HeightMm is >0 and <=2000 && Hash(p.PngSha256) && Hash(p.PdfSha256) && p.ProductIds.Count<=1000), "Catalogue edition page metadata is invalid.");
        }
        Require(snapshot.Collections.All(c => c is not null && c.Names is not null && c.ProductIds is not null && c.ProductIds.All(id => snapshot.Products.Any(p => p.Document.Id == id))), "Collections must refer to existing products.");
        Require(snapshot.Collections.Select(c => c.Id).Distinct().Count() == snapshot.Collections.Count, "Collection IDs must be unique.");
        Require(snapshot.Logos.All(l => Hash(l.Value?.Sha256) && l.Value!.ContentType is "image/png" or "image/jpeg" or "image/webp"), "Logos require supported image hashes.");
        Require(snapshot.MediaApprovals.All(m => Hash(m.Key) && m.Value is not null && m.Value.Kind is "unknown" or "photo" or "visualization" or "archival"
            && (!m.Value.ApprovedForPublication || m.Value.Kind != "unknown") && (m.Value.SourceUrl is null || Https(m.Value.SourceUrl))), "Approved media requires a known provenance kind and safe source URL.");
    }
    public static void ValidateCreativeInput(CreativeImportInput request)
    {
        Require(request.Source is {Application:"ocharo-studio",Revision:>=0} && !string.IsNullOrWhiteSpace(request.Source.WorkspaceId) && !string.IsNullOrWhiteSpace(request.Source.LookId),"Studio source identity is required.");
        Require(request.Derivative is not null && CommerceRules.Hash(request.Derivative.Sha256) && request.Derivative.MediaType is "image/png" or "image/jpeg","A verified derivative image is required.");
        Require(request.AssetReleases is not null && request.AssetReleases.Count<=100 && request.AssetReleases.All(a=>a is not null&&CommerceRules.Hash(a.Sha256)&&!string.IsNullOrWhiteSpace(a.Id)),"Asset release evidence is invalid.");
        Require(request.Materials is not null && request.Materials.Count<=100 && request.Materials.All(m=>m is not null&&!string.IsNullOrWhiteSpace(m.Slot)&&!string.IsNullOrWhiteSpace(m.Source)&&(m.Sha256 is null||CommerceRules.Hash(m.Sha256))),"Material evidence is invalid.");
    }
    public static CommerceSnapshot RestrictEditorApprovals(CommerceSnapshot next, CommerceSnapshot? previous)
    {
        var products = next.Products.Select(item => {
            var old = previous?.Products.FirstOrDefault(p => p.Document.Id == item.Document.Id)?.Document;
            ProductLocale Locale(ProductLocale value, ProductLocale? prior) => value == prior ? value : value with { Status = "draft" };
            var doc = item.Document with {
                Locales = new(Locale(item.Document.Locales.Uz, old?.Locales.Uz), Locale(item.Document.Locales.Ru, old?.Locales.Ru), Locale(item.Document.Locales.En, old?.Locales.En)),
                Pricing = item.Document.Pricing == old?.Pricing ? item.Document.Pricing : item.Document.Pricing is {} price ? price with { Status = "draft" } : null
            };
            return item with { Document = doc };
        }).ToArray();
        var links = next.Identity.Links.Where(link => previous?.Identity.Links.Contains(link) == true && previous.VerifiedLinkKeys.Contains(link.Key)).Select(l => l.Key).ToArray();
        return next with { Products = products, VerifiedLinkKeys = links,
            MediaApprovals = next.MediaApprovals.ToDictionary(m => m.Key, m => previous?.MediaApprovals.GetValueOrDefault(m.Key) == m.Value ? m.Value : m.Value with { ApprovedForPublication = false }) };
    }
    public static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new PreparationException("validation", message); }
    public static bool Https(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
    public static IEnumerable<string> ApprovedHashes(CommerceSnapshot s) => s.Logos.Values.Select(l => l.Sha256).Concat(s.Products.Where(p => p.Document.Status == "active" && p.Document.Issues.All(i => i.Status != "open")).SelectMany(p => p.Document.Media).Where(m => m.Role != "reference").Select(m => m.Sha256)).Distinct().Where(h => s.MediaApprovals.TryGetValue(h, out var approval) && approval.ApprovedForPublication);
}
