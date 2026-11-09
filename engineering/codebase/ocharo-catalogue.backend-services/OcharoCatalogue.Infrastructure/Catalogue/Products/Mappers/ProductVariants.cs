using System.Text.RegularExpressions;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;

/// <summary>Maps a garment family's products to its next free variant code.</summary>
public static partial class ProductVariants
{
    /// <summary>
    /// Gets the variant after the highest <c>-vNN</c> id of <paramref name="garmentType"/>, such as <c>v03</c>.
    /// </summary>
    /// <param name="existing">Every product row.</param>
    /// <param name="garmentType">The garment family key.</param>
    public static string Next(IEnumerable<ProductRecord> existing, string garmentType)
    {
        var highest = existing
            .Where(record => record.GarmentType == garmentType)
            .Select(record => VariantNumber().Match(record.Id))
            .Where(match => match.Success)
            .Select(match => int.Parse(match.Groups[1].Value))
            .DefaultIfEmpty(0)
            .Max();
        return $"v{highest + 1:00}";
    }

    [GeneratedRegex(@"-v(\d+)$")]
    private static partial Regex VariantNumber();
}
