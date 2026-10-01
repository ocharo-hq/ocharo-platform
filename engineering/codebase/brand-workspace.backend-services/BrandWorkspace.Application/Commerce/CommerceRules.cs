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
            Require(item.Translations is null || item.Translations.Count <= CatalogueLanguages.Codes.Count - 3
                && item.Translations.All(entry => CatalogueLanguages.Supported(entry.Key) && !CatalogueLanguages.Core(entry.Key) && entry.Value is not null),
                "Additional product translations must use supported non-core languages.");
            foreach (var locale in new[] { p.Locales!.Uz, p.Locales.Ru, p.Locales.En }.Concat(item.Translations?.Values ?? []))
                Require(locale.Status is "draft" or "approved" && (locale.Status != "approved" || !string.IsNullOrWhiteSpace(locale.Name))
                    && (locale.Name?.Length ?? 0) <= 300 && (locale.Fabric?.Length ?? 0) <= 1000
                    && (locale.Decoration?.Length ?? 0) <= 1000 && (locale.Description?.Length ?? 0) <= 10000,
                    "Product copy exceeds supported limits or approved copy is missing a title.");
            Require(p.Media.All(m => m is not null && Hash(m.Sha256) && m.ContentType is "image/png" or "image/jpeg" or "image/webp"), "Product images need valid hashes and supported image types.");
            Require(p.Media.Select(m => m.Id).Distinct().Count() == p.Media.Count && p.Media.All(m => !string.IsNullOrWhiteSpace(m.Id) && m.Id.Length <= 120), "Product image IDs must be distinct.");
            Require(item.MediaGroups is null || item.MediaGroups.Count <= 100 && item.MediaGroups.All(g => g is not null)
                && item.MediaGroups.Select(g => g.Id).Distinct().Count() == item.MediaGroups.Count, "Media group IDs must be distinct.");
            foreach (var group in item.MediaGroups ?? [])
                Require(group.Id is { Length: > 0 and <= 100 } && SlugPattern().IsMatch(group.Id)
                    && !string.IsNullOrWhiteSpace(group.Label) && group.Label.Length <= 120
                    && group.Category is "model" or "product" or "detail" or "fabric"
                    && group.MediaIds is not null && group.MediaIds.Count <= 500
                    && group.MediaIds.Distinct().Count() == group.MediaIds.Count
                    && group.MediaIds.All(id => p.Media.Any(m => m.Id == id && m.Role != "reference")),
                    "Media groups must reference distinct non-reference images attached to the same product.");
            Require(p.Stock is null || p.Stock.All(s => p.Sizes.Contains(s.Key) && s.Value >= 0), "Stock must use known sizes and nonnegative counts.");
            if (p.Pricing is { } price)
                Require(price.Status is "draft" or "confirmed" && price.Currency is { Length: 3 } && price.Currency.All(char.IsAsciiLetterUpper)
                    && price.Tiers is not null && price.Tiers.All(t => t.MinimumQuantity > 0 && t.UnitPrice >= 0 && (t.MaximumQuantity is null || t.MaximumQuantity >= t.MinimumQuantity))
                    && price.Tiers.Count(t => t.MinimumQuantity == 1) <= 1, "Pricing must use a currency and valid nonnegative tiers.");
        }
        Require(snapshot.CatalogueEditions is not null && snapshot.CatalogueEditions.Count <= 200 && snapshot.CatalogueEditions.Select(e=>(e.Slug,e.Locale)).Distinct().Count()==snapshot.CatalogueEditions.Count, "Catalogue editions must have distinct names and languages (maximum 200).");
        foreach(var e in snapshot.CatalogueEditions)
        {
            Require(e.Id!=Guid.Empty && e.SourceDesignId!=Guid.Empty && e.SourceRevision>0 && CatalogueLanguages.Supported(e.Locale) && !string.IsNullOrWhiteSpace(e.Title) && e.Title.Length<=160 && e.TemplateId.Length<=120 && (e.Slug is null || CatalogueDefinition.ValidSlug(e.Slug)) && e.Description.Length<=2000 && (e.PdfSha256 is null || Hash(e.PdfSha256)), "Catalogue edition metadata is invalid.");
            Require(e.Pages.Count<=100 && (e.Pages.Count>0)==(e.PdfSha256 is not null) && e.Pages.Select(p=>p.Id).Distinct().Count()==e.Pages.Count, "Catalogue edition pages must be distinct.");
            Require(e.Pages.All(p=>p.Id.Length is >0 and <=100 && p.Id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_') && p.Title.Length<=160 && double.IsFinite(p.WidthMm) && double.IsFinite(p.HeightMm) && p.WidthMm is >0 and <=2000 && p.HeightMm is >0 and <=2000 && Hash(p.PngSha256) && Hash(p.PdfSha256) && p.ProductIds.Count<=1000), "Catalogue edition page metadata is invalid.");
            Require(e.Pages.SelectMany(page=>page.ProductIds).All(id=>snapshot.Products.Any(product=>product.Document.Id==id)), "Catalogue pages must reference products in this brand.");
            if(e.Web is {} web)
            {
                Require(web.MediaGroup is "all" or "model" or "product" or "detail" or "fabric" && web.PriceDisplay is not null && web.Copy is not null
                    && web.Copy.Count<=10 && web.Copy.All(copy=>CatalogueWebSettings.CopyKeys.Contains(copy.Key) && copy.Value is not null && copy.Value.Length<=1000)
                    && web.ProductMedia is not null && web.ProductMedia.All(entry=>snapshot.Products.Any(product=>product.Document.Id==entry.Key) && entry.Value.Count<=500 && entry.Value.All(Hash)),"Web catalogue presentation settings are invalid.");
                Require(web.Theme is "editorial" or "grid" && web.Pages.Count<=100 && web.Pages.Select(page=>page.Id).Distinct().Count()==web.Pages.Count,"Web catalogue metadata is invalid.");
                Require(web.Pages.All(page=>page.Id.Length is >0 and <=100 && page.Title.Length is >0 and <=160 && page.Layout is "spread" or "grid" or "collage" && page.ProductIds.Count<=1000 && page.ProductIds.All(id=>snapshot.Products.Any(product=>product.Document.Id==id))),"Web catalogue pages must reference products in this brand.");
            }
            Require(e.PdfSha256 is not null || e.Web?.Pages.Any(page=>page.IsActive && page.ProductIds.Count>0)==true,"Catalogue needs a PDF or web output.");
        }
        Require(snapshot.Collections.All(c => c is not null && c.Names is not null && c.ProductIds is not null && c.ProductIds.All(id => snapshot.Products.Any(p => p.Document.Id == id))), "Collections must refer to existing products.");
        Require(snapshot.Collections.Select(c => c.Id).Distinct().Count() == snapshot.Collections.Count, "Collection IDs must be unique.");
        Require(snapshot.Logos.All(l => Hash(l.Value?.Sha256) && l.Value!.ContentType is "image/png" or "image/jpeg" or "image/webp"), "Logos require supported image hashes.");
        Require(snapshot.MediaApprovals.All(m => Hash(m.Key) && m.Value is not null && m.Value.Kind is "unknown" or "photo" or "visualization" or "archival"
            && (!m.Value.ApprovedForPublication || m.Value.Kind != "unknown") && (m.Value.SourceUrl is null || Https(m.Value.SourceUrl))), "Approved media requires a known provenance kind and safe source URL.");
    }
    public static void ValidateEditedPrices(CommerceSnapshot next, CommerceSnapshot? previous)
    {
        foreach(var product in next.Products)
        {
            var price=product.Document.Pricing;
            var prior=previous?.Products.FirstOrDefault(item=>item.Document.Id==product.Document.Id)?.Document.Pricing;
            if(price is null || SamePricing(price,prior))continue;
            Require(price.Currency=="UZS" && price.Tiers.Count==2
                && price.Tiers[0].MinimumQuantity==1 && price.Tiers[0].MaximumQuantity==9
                && price.Tiers[1].MinimumQuantity==10 && price.Tiers[1].MaximumQuantity is null,
                "Edited prices must use UZS with two tiers: 1–9 pieces and 10+ pieces.");
            Require(price.Status!="confirmed" || price.Tiers.All(tier=>tier.UnitPrice>0),
                "Enter positive UZS amounts for both price tiers before confirming prices.");
        }
    }
    public static bool EditionEligible(CatalogueEdition edition, CommerceSnapshot snapshot)
    {
        bool Eligible(string id)=>snapshot.Products.Any(product=>product.Document.Id==id && ProductPublicationEligibility.Eligible(product,edition.Locale));
        return edition.IsActive && (PdfEligible(edition,snapshot) || edition.Web?.Pages.Any(page=>page.IsActive && page.ProductIds.Any(Eligible))==true);
    }
    public static bool PdfEligible(CatalogueEdition edition, CommerceSnapshot snapshot) => edition.IsActive && edition.PdfSha256 is not null
        && edition.Pages.SelectMany(page=>page.ProductIds).All(id=>snapshot.Products.Any(product=>product.Document.Id==id && ProductPublicationEligibility.Eligible(product,edition.Locale)));
    public static void ValidateCreativeInput(CreativeImportInput request)
    {
        Require(request.Source is {Application:"ocharo-studio",Revision:>=0} && !string.IsNullOrWhiteSpace(request.Source.WorkspaceId) && !string.IsNullOrWhiteSpace(request.Source.LookId),"Studio source identity is required.");
        Require(request.Derivative is not null && CommerceRules.Hash(request.Derivative.Sha256) && request.Derivative.MediaType is "image/png" or "image/jpeg","A verified derivative image is required.");
        Require(request.AssetReleases is not null && request.AssetReleases.Count<=100 && request.AssetReleases.All(a=>a is not null&&CommerceRules.Hash(a.Sha256)&&!string.IsNullOrWhiteSpace(a.Id)),"Asset release evidence is invalid.");
        Require(request.Materials is not null && request.Materials.Count<=100 && request.Materials.All(m=>m is not null&&!string.IsNullOrWhiteSpace(m.Slot)&&!string.IsNullOrWhiteSpace(m.Source)&&(m.Sha256 is null||CommerceRules.Hash(m.Sha256))),"Material evidence is invalid.");
    }
    private static bool SamePricing(ProductPricing? left, ProductPricing? right) => ReferenceEquals(left,right)
        || left is not null && right is not null && left.Currency==right.Currency && left.Status==right.Status
        && left.Wording==right.Wording && left.Tiers.SequenceEqual(right.Tiers);
    public static CommerceSnapshot RestrictEditorApprovals(CommerceSnapshot next, CommerceSnapshot? previous)
    {
        var products = next.Products.Select(item => {
            var old = previous?.Products.FirstOrDefault(p => p.Document.Id == item.Document.Id)?.Document;
            ProductLocale Locale(ProductLocale value, ProductLocale? prior) => value == prior ? value : value with { Status = "draft" };
            var doc = item.Document with {
                Locales = new(Locale(item.Document.Locales.Uz, old?.Locales.Uz), Locale(item.Document.Locales.Ru, old?.Locales.Ru), Locale(item.Document.Locales.En, old?.Locales.En)),
                Pricing = SamePricing(item.Document.Pricing,old?.Pricing) ? item.Document.Pricing : item.Document.Pricing is {} price ? price with { Status = "draft" } : null
            };
            return item with { Document = doc, Translations = item.Translations?.ToDictionary(entry => entry.Key,
                entry => Locale(entry.Value, previous?.Products.FirstOrDefault(p => p.Document.Id == item.Document.Id)?.Translations?.GetValueOrDefault(entry.Key))) };
        }).ToArray();
        var links = next.Identity.Links.Where(link => previous?.Identity.Links.Contains(link) == true && previous.VerifiedLinkKeys.Contains(link.Key)).Select(l => l.Key).ToArray();
        return next with { Products = products, VerifiedLinkKeys = links,
            MediaApprovals = next.MediaApprovals.ToDictionary(m => m.Key, m => previous?.MediaApprovals.GetValueOrDefault(m.Key) == m.Value ? m.Value : m.Value with { ApprovedForPublication = false }) };
    }
    public static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new PreparationException("validation", message); }
    public static bool Https(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
    public static IEnumerable<string> ApprovedHashes(CommerceSnapshot s) => s.Logos.Values.Select(l => l.Sha256).Concat(s.Products.Where(p => p.Document.Status == "active" && p.Document.Issues.All(i => i.Status != "open")).SelectMany(p => p.Document.Media).Where(m => m.Role != "reference").Select(m => m.Sha256)).Distinct().Where(h => s.MediaApprovals.TryGetValue(h, out var approval) && approval.ApprovedForPublication);
}
