using System.Security.Cryptography;
using System.Text.Json;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Domain.DesignDocuments.Models;
using SkiaSharp;

namespace BrandWorkspace.Infrastructure.Commerce;

/// <summary>Binds uploaded rendered artifacts to metadata from an immutable, tenant-scoped saved design.</summary>
public static class CatalogueEditionBuilder
{
    public const int MaximumPdfBytes=64*1024*1024;
    public const int MaximumPageBytes=15*1024*1024;
    public const int MaximumTotalBytes=256*1024*1024;
    public static (CatalogueEdition Edition,IReadOnlyList<CommerceBlob> Blobs) Build(DesignDocument design,Guid brandId,
        IReadOnlyDictionary<string,byte[]> files,DateTimeOffset now)
    {
        CommerceRules.Require(design.Kind=="catalogue" && !design.Archived && design.Revision>0,"Choose a saved catalogue revision.");
        var snapshot=design.Snapshot;
        CommerceRules.Require(snapshot.ValueKind==JsonValueKind.Object && Text(snapshot,"kind")=="catalogue", "Saved catalogue data is invalid.");
        var locale=Text(snapshot,"locale");
        CommerceRules.Require(CatalogueLanguages.Supported(locale),"Choose a supported catalogue language.");
        // Connected original layouts store their canonical brand at document level.
        // Older designs retain the existing per-page source checks below.
        if(snapshot.TryGetProperty("catalogueSettings",out var settings) && settings.ValueKind==JsonValueKind.Object
            && settings.TryGetProperty("brandId",out _))
            CommerceRules.Require(Guid.TryParse(Text(settings,"brandId"),out var settingsBrand) && settingsBrand==brandId,
                "The saved catalogue belongs to another brand.");
        var templateId=Text(snapshot,"templateId");
        CommerceRules.Require(templateId.Length is >0 and <=120,"Saved catalogue template is invalid.");
        CommerceRules.Require(snapshot.TryGetProperty("pages",out var sourcePages) && sourcePages.ValueKind==JsonValueKind.Array && sourcePages.GetArrayLength() is >0 and <=100,"Saved catalogue needs 1 to 100 pages.");
        CommerceRules.Require(files.Values.Sum(b=>(long)b.Length)<=MaximumTotalBytes,"Catalogue artifacts exceed the total upload limit.");
        var expected=new HashSet<string>(StringComparer.Ordinal){"pdf"};
        var pages=new List<CatalogueEditionPage>();var blobs=new List<CommerceBlob>();
        foreach(var page in sourcePages.EnumerateArray())
        {
            var id=Text(page,"id");var title=Text(page,"title");
            CommerceRules.Require(id.Length is >0 and <=100 && id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_') && title.Length<=160 && !pages.Any(p=>p.Id==id),"Catalogue page identifiers must be safe and distinct.");
            var width=Number(page,"width");var height=Number(page,"height");
            CommerceRules.Require(width is >0 and <=2000 && height is >0 and <=2000,"Catalogue page dimensions are invalid.");
            var hasProducts=page.TryGetProperty("productIds",out var products);
            CommerceRules.Require(!hasProducts || products.ValueKind==JsonValueKind.Array && products.GetArrayLength()<=1000 && products.EnumerateArray().All(v=>v.ValueKind==JsonValueKind.String && v.GetString()!.Length is >0 and <=120),"Catalogue page product references are invalid.");
            if(page.TryGetProperty("catalogueSource",out var origin) && origin.ValueKind!=JsonValueKind.Null)
                CommerceRules.Require(Guid.TryParse(Text(origin,"brandId"),out var originBrand) && originBrand==brandId,"The saved page belongs to another brand.");
            var pngName=$"page-{id}.png";var pdfName=$"page-{id}.pdf";expected.Add(pngName);expected.Add(pdfName);
            var png=File(files,pngName,MaximumPageBytes);var pdf=File(files,pdfName,MaximumPageBytes);
            ValidatePng(png,width/height);ValidatePdf(pdf);
            var pngHash=Hash(png);var pdfHash=Hash(pdf);blobs.Add(new(pngHash,"image/png",png));blobs.Add(new(pdfHash,"application/pdf",pdf));
            pages.Add(new(id,title,width,height,hasProducts?products.EnumerateArray().Select(v=>v.GetString()!).Distinct().ToArray():[],pngHash,pdfHash));
        }
        CommerceRules.Require(expected.SetEquals(files.Keys),"Upload the full PDF and exactly one PNG and PDF for every saved page.");
        var fullPdf=File(files,"pdf",MaximumPdfBytes);ValidatePdf(fullPdf);var fullHash=Hash(fullPdf);blobs.Add(new(fullHash,"application/pdf",fullPdf));
        return (new(Guid.NewGuid(),design.Id,design.Revision,design.Title,locale,templateId,pages,fullHash,false,now),blobs);
    }
    private static string Text(JsonElement value,string name)
    {
        CommerceRules.Require(value.ValueKind==JsonValueKind.Object && value.TryGetProperty(name,out var field) && field.ValueKind==JsonValueKind.String,$"Saved catalogue {name} is required.");
        return value.GetProperty(name).GetString()!;
    }
    private static double Number(JsonElement value,string name)
    {
        CommerceRules.Require(value.TryGetProperty(name,out var field) && field.ValueKind==JsonValueKind.Number && field.TryGetDouble(out var n) && double.IsFinite(n),$"Saved catalogue {name} is invalid.");
        return value.GetProperty(name).GetDouble();
    }
    private static byte[] File(IReadOnlyDictionary<string,byte[]> files,string key,int limit)
    {
        CommerceRules.Require(files.TryGetValue(key,out var bytes) && bytes.Length>0 && bytes.Length<=limit,$"Missing or oversized catalogue artifact: {key}.");
        return bytes!;
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void ValidatePdf(byte[] bytes)
    {
        CommerceRules.Require(bytes.Length>=16 && bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8) && bytes.AsSpan(Math.Max(0,bytes.Length-1024)).IndexOf("%%EOF"u8)>=0,"Catalogue PDFs must have a valid PDF header and end marker.");
    }
    private static void ValidatePng(byte[] bytes,double ratio)
    {
        ReadOnlySpan<byte> end=[0,0,0,0,73,69,78,68,174,66,96,130];
        CommerceRules.Require(bytes.Length>=end.Length && bytes.AsSpan(bytes.Length-end.Length).SequenceEqual(end),"Catalogue PNG has an invalid ending.");
        using var data=SKData.CreateCopy(bytes);using var codec=SKCodec.Create(data);
        CommerceRules.Require(codec is not null && codec.EncodedFormat==SKEncodedImageFormat.Png,"Catalogue page previews must be PNG images.");
        var info=codec!.Info;
        CommerceRules.Require(info.Width is >0 and <=8192 && info.Height is >0 and <=8192 && (long)info.Width*info.Height<=32_000_000 && codec.FrameCount<=1,"Catalogue previews must be single images up to 8192 pixels and 32 megapixels.");
        CommerceRules.Require(Math.Abs((double)info.Width/info.Height/ratio-1)<=0.02,"Page preview proportions differ from the saved page dimensions.");
        using var decoded=new SKBitmap(new SKImageInfo(info.Width,info.Height,SKColorType.Rgba8888,SKAlphaType.Premul));
        CommerceRules.Require(codec.GetPixels(decoded.Info,decoded.GetPixels())==SKCodecResult.Success,"Catalogue PNG could not be completely decoded.");
    }
}
