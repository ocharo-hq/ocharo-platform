using System.Security.Cryptography;
using System.Text;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Hashers;

/// <summary>Hashes the products and settings a release reads, to detect source edits between rendering, preparation and
/// publication; the release's own transaction recomputes it.</summary>
public static class DesignFingerprint
{
    /// <summary>
    /// Creates the fingerprint of <paramref name="productIds"/> within <paramref name="products"/> and the settings.
    /// </summary>
    /// <param name="products">Every product row.</param>
    /// <param name="settings">The settings row; null until first saved.</param>
    /// <param name="productIds">The products the release reads.</param>
    public static string Create(
        IEnumerable<ProductRecord> products,
        SettingsRecord? settings,
        IEnumerable<string> productIds)
    {
        var wanted = productIds.ToHashSet(StringComparer.Ordinal);
        var rows = products
            .Where(product => wanted.Contains(product.Id))
            .OrderBy(product => product.Id, StringComparer.Ordinal)
            .ToArray();
        if (rows.Length != wanted.Count)
        {
            throw CatalogueErrors.Invalid("A catalogue product no longer exists.").ToException();
        }

        var data = CatalogueJson.Write(new
        {
            products = rows.Select(product => new { product.Id, product.Revision, product.Json }),
            settings = settings?.Json,
            settingsRevision = settings?.Revision,
        });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }
}
