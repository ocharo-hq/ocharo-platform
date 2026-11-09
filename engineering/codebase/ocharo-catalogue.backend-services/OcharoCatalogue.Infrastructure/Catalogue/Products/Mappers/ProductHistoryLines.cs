using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;

/// <summary>Maps a product save to the line its revision history shows.</summary>
public static class ProductHistoryLines
{
    /// <summary>Gets the history line of an edit: the fields that changed, or <c>Saved</c>.</summary>
    /// <param name="before">The document before the edit.</param>
    /// <param name="after">The document after the edit.</param>
    public static string Describe(ProductDocument before, ProductDocument after)
    {
        var changes = new List<string>();
        if (before.Status != after.Status)
        {
            changes.Add($"status {before.Status} → {after.Status}");
        }

        if (before.Locales != after.Locales)
        {
            changes.Add("copy");
        }

        if (!ProductInputNormalizer.SamePricing(before.Pricing, after.Pricing))
        {
            changes.Add("pricing");
        }

        if (!before.Sizes.SequenceEqual(after.Sizes))
        {
            changes.Add("sizes");
        }

        if (!before.Media.SequenceEqual(after.Media))
        {
            changes.Add("images");
        }

        if (!before.Issues.SequenceEqual(after.Issues))
        {
            changes.Add("review notes");
        }

        if (!before.Tags.SequenceEqual(after.Tags))
        {
            changes.Add("tags");
        }

        return changes.Count == 0 ? "Saved" : "Edited " + string.Join(", ", changes);
    }

    /// <summary>
    /// Gets the requested history line, cut to 200 characters, or <paramref name="fallback"/> when none.
    /// </summary>
    /// <param name="requested">The line the caller sent.</param>
    /// <param name="fallback">The line derived from the save.</param>
    public static string Summary(string? requested, string fallback)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return fallback;
        }

        var trimmed = requested.Trim();
        return trimmed[..Math.Min(200, trimmed.Length)];
    }
}
