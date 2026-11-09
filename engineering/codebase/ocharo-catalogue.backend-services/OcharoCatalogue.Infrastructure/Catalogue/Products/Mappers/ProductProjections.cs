using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;

/// <summary>Maps stored products to the rows the grids read, and product documents back to rows.</summary>
public static class ProductProjections
{
    /// <summary>Maps a stored product to its grid row.</summary>
    /// <param name="document">The product document.</param>
    /// <param name="record">The product row.</param>
    /// <param name="collectionIds">The collections holding the product.</param>
    public static ProductSummary ToSummary(
        ProductDocument document,
        ProductRecord record,
        IReadOnlyList<string>? collectionIds = null)
    {
        var primary = PrimaryImage(document);
        var languages = AuthoredLanguages(document.Locales);
        var approved = languages.Count(code => document.Locales.For(code).Status == LocaleStatus.Approved
            && !string.IsNullOrWhiteSpace(document.Locales.For(code).Name));
        return new ProductSummary(
            document.Id,
            document.GarmentType,
            document.VariantCode,
            document.Status,
            document.SortOrder,
            document.Locales.Uz.Name,
            document.Locales.Ru.Name,
            document.Locales.En.Name,
            document.Pricing?.RetailPrice,
            document.Pricing?.WholesalePrice,
            document.Pricing?.Status,
            approved,
            document.Issues.Count(issue => issue.Status == IssueStatus.Open),
            document.Media.Count,
            primary is null ? null : CatalogueUrls.Media(primary.Sha256),
            PrimaryPreviewUrl(document),
            document.Tags,
            record.Revision,
            record.UpdatedAt,
            document.Availability,
            document.Stock?.Values.Sum(),
            languages.Count,
            languages.Count - approved,
            collectionIds ?? []);
    }

    /// <summary>Maps a product to its stock sheet row.</summary>
    /// <param name="document">The product document.</param>
    /// <param name="status">The row's status.</param>
    /// <param name="revision">The row's revision.</param>
    public static StockRow ToStockRow(
        ProductDocument document,
        string status,
        int revision)
    {
        return new StockRow(
            document.Id,
            document.GarmentType,
            status,
            revision,
            document.Locales.Uz.Name,
            PrimaryPreviewUrl(document),
            document.Sizes,
            document.Stock,
            document.MadeToOrder,
            document.Availability);
    }

    /// <summary>Maps a product to its copy grid row.</summary>
    /// <param name="document">The product document.</param>
    /// <param name="record">The product row.</param>
    public static ProductCopyRow ToCopyRow(ProductDocument document, ProductRecord record)
    {
        return new ProductCopyRow(
            record.Id,
            record.GarmentType,
            record.Status,
            record.Revision,
            PrimaryPreviewUrl(document),
            document.Locales);
    }

    /// <summary>Maps a product document to its row at <paramref name="revision"/>.</summary>
    /// <param name="document">The product document.</param>
    /// <param name="revision">The row's revision.</param>
    /// <param name="created">When the product was created.</param>
    /// <param name="updated">When this revision was saved.</param>
    public static ProductRecord ToRecord(
        ProductDocument document,
        int revision,
        DateTimeOffset created,
        DateTimeOffset updated)
    {
        return new ProductRecord
        {
            Id = document.Id,
            GarmentType = document.GarmentType,
            Status = document.Status,
            SortOrder = document.SortOrder,
            Revision = revision,
            CreatedAt = created,
            UpdatedAt = updated,
            Json = CatalogueJson.Write(document),
        };
    }

    /// <summary>Gets the first cutout, else the first model image, else the first image; active images only.</summary>
    /// <param name="document">The product document.</param>
    public static ProductMedia? PrimaryImage(ProductDocument document)
    {
        var images = ProductPublicationEligibility.ActiveImages(document);
        return images.FirstOrDefault(image => image.Role == MediaRole.Cutout)
            ?? images.FirstOrDefault(image => image.Role == MediaRole.Model)
            ?? images.FirstOrDefault();
    }

    /// <summary>Gets the URL a grid shows the product by: the primary image's preview, else its original.</summary>
    /// <param name="document">The product document.</param>
    public static string? PrimaryPreviewUrl(ProductDocument document)
    {
        var primary = PrimaryImage(document);
        if (primary is null)
        {
            return null;
        }

        return primary.HasPreview ? CatalogueUrls.Preview(primary.Sha256) : CatalogueUrls.Media(primary.Sha256);
    }

    private static IReadOnlyList<string> AuthoredLanguages(ProductLocales locales)
    {
        return CatalogueLocales.All
            .Where(code => code is "uz" or "ru" or "en"
                || (locales.Additional?.ContainsKey(code) == true && IsAuthored(locales.For(code))))
            .ToList();
    }

    private static bool IsAuthored(ProductLocale locale)
    {
        return locale.Status == LocaleStatus.Approved
            || new[] { locale.Name, locale.Fabric, locale.Decoration, locale.Description }
                .Any(value => !string.IsNullOrWhiteSpace(value));
    }
}
