using System.Security.Cryptography;
using System.Text;
using OcharoCatalogue.Domain.Catalogue.Entities;
namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Detects source edits between rendering, preparation and publication.</summary>
public static class DesignFingerprint
{
    public static string Create(IEnumerable<ProductRecord> products, SettingsRecord? settings, IEnumerable<string> productIds)
    {
        var wanted=productIds.ToHashSet(StringComparer.Ordinal);
        var rows=products.Where(p=>wanted.Contains(p.Id)).OrderBy(p=>p.Id,StringComparer.Ordinal).ToArray();
        if(rows.Length!=wanted.Count) throw new CatalogueException("A catalogue product no longer exists.");
        var data=CatalogueJson.Write(new {products=rows.Select(p=>new {p.Id,p.Revision,p.Json}),settings=settings?.Json,settingsRevision=settings?.Revision});
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    }
}
