using System.Security.Cryptography;
using System.Text.Json;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Prepares immutable catalogue outputs from saved local designs and reviewed product revisions.</summary>
public sealed class CatalogueReleaseService(IDesignRepository designs,ICatalogueRepository repository,SettingsService settings,IDesignArtifactValidator artifacts,IMediaStore media,IClock clock)
{
    public const int MaximumTotalBytes=256*1024*1024;
    private sealed record Preparation(CatalogueDesignRecord Design,JsonElement Snapshot,CatalogueDefinition Definition,IReadOnlyList<ProductRecord> Rows,IReadOnlyDictionary<string,ProductDocument> Products,IReadOnlyList<string> References,string Fingerprint);
    private async Task<Preparation> ReadAsync(Guid id,int? expected,CancellationToken ct)
    {
        var d=await designs.GetAsync(id,ct)??throw CatalogueException.NotFound("Catalogue design");
        if(d.Archived||expected is not null&&d.Revision!=expected)throw CatalogueException.Conflict("The catalogue changed or was archived. Reload before preparing.");
        var snapshot=CatalogueDesignValidation.Validate(new(d.Title,CatalogueJson.Read<JsonElement>(d.Json)));
        var definition=CatalogueDesignValidation.Definition(snapshot);CatalogueDesignValidation.Require(definition.IsActive,"Activate the catalogue before preparing a release.");
        var rows=await repository.GetProductsAsync(ct);var products=rows.ToDictionary(p=>p.Id,p=>CatalogueJson.Read<ProductDocument>(p.Json),StringComparer.Ordinal);
        var references=CatalogueDesignValidation.References(snapshot).Concat(snapshot.GetProperty("pages").EnumerateArray().SelectMany(page=>References(page,products))).Distinct().ToArray();
        if(definition.Outputs.Pdf)
        foreach(var page in snapshot.GetProperty("pages").EnumerateArray().Where(CatalogueDesignValidation.Active))
        foreach(var productId in References(page,products))
        {
            var sourceRevision=0;
            if(snapshot.TryGetProperty("productRevisions",out var evidence)&&evidence.TryGetProperty(productId,out var saved))saved.TryGetInt32(out sourceRevision);
            if(sourceRevision==0&&page.TryGetProperty("catalogueSource",out var origin)&&origin.TryGetProperty("productId",out var sourceId)&&sourceId.GetString()==productId&&origin.TryGetProperty("productRevision",out var productRevision))productRevision.TryGetInt32(out sourceRevision);
            if(sourceRevision==0||rows.All(row=>row.Id!=productId||row.Revision!=sourceRevision))throw CatalogueException.Conflict("PDF product information is stale. Refresh the products and save the catalogue before preparing.");
        }
        var fingerprint=DesignFingerprint.Create(rows,await repository.FindSettingsAsync(ct),references);
        var preparation=new Preparation(d,snapshot,definition,rows,products,references,fingerprint);
        var pdfHashes=PdfPages(preparation).SelectMany(page=>page.GetProperty("elements").EnumerateArray()).Where(e=>e.TryGetProperty("src",out _)).Select(e=>CatalogueDesignValidation.ImageHash(e.GetProperty("src").GetString()!)).Where(hash=>hash is not null).Select(hash=>hash!);
        var webHashes=definition.Outputs.Web?definition.WebPages.Where(page=>page.IsActive).SelectMany(page=>page.ProductIds).Distinct().Where(id=>Eligible(preparation,id)).SelectMany(id=>ProductPublicationEligibility.ActiveImages(products[id])).Select(image=>image.Sha256):[];
        await VerifyMediaAsync(pdfHashes.Concat(webHashes),ct);
        return preparation;
    }
    public async Task<object> PreparationAsync(Guid id,CancellationToken ct)
    {
        var p=await ReadAsync(id,null,ct);return new {designRevision=p.Design.Revision,fingerprint=p.Fingerprint,pdfPageIds=PdfPages(p).Select(page=>page.GetProperty("id").GetString()).ToArray(),excludedProductIds=p.References.Where(id=>!Eligible(p,id)).ToArray()};
    }
    public async Task<CataloguePreparedRelease> PrepareAsync(Guid id,int expected,string fingerprint,IReadOnlyDictionary<string,byte[]> files,CancellationToken ct)
    {
        var p=await ReadAsync(id,expected,ct);if(p.Fingerprint!=fingerprint)throw CatalogueException.Conflict("Product information changed while rendering. Review and render again.");
        CatalogueDesignValidation.Require(files.Values.Sum(v=>(long)v.Length)<=MaximumTotalBytes,"Catalogue artifacts exceed 256 MiB.");
        var expectedFiles=new HashSet<string>(StringComparer.Ordinal);var assets=new List<CatalogueReleaseAssetRecord>();var links=new Dictionary<string,string>(StringComparer.Ordinal);var pages=new List<CatalogueReleasePage>();
        void Add(string name,string type,int max,double? ratio=null,IReadOnlyList<CataloguePdfPageSize>? pdfPages=null)
        {
            expectedFiles.Add(name);CatalogueDesignValidation.Require(files.TryGetValue(name,out var bytes)&&bytes.Length>0&&bytes.Length<=max,$"Missing or oversized catalogue artifact: {name}.");
            if(type=="application/pdf")artifacts.ValidatePdf(bytes!,pdfPages!);else artifacts.ValidatePng(bytes!,ratio!.Value);
            var hash=Convert.ToHexStringLower(SHA256.HashData(bytes!));assets.Add(new(){Sha256=hash,ContentType=type,Bytes=bytes!});links[name]=hash;
        }
        foreach(var page in PdfPages(p))
        {
            var pageId=page.GetProperty("id").GetString()!;var width=page.GetProperty("width").GetDouble();var height=page.GetProperty("height").GetDouble();
            Add($"page-{pageId}.png","image/png",15*1024*1024,width/height);Add($"page-{pageId}.pdf","application/pdf",15*1024*1024,pdfPages:[new(width,height)]);
            pages.Add(new(pageId,page.GetProperty("title").GetString()!,width,height,References(page,p.Products),$"page-{pageId}.png",$"page-{pageId}.pdf"));
        }
        string? pdf=null;if(p.Definition.Outputs.Pdf){CatalogueDesignValidation.Require(pages.Count>0,"No active PDF pages contain confirmed products in this language.");pdf="catalogue.pdf";Add(pdf,"application/pdf",64*1024*1024,pdfPages:pages.Select(page=>new CataloguePdfPageSize(page.WidthMm,page.HeightMm)).ToArray());}
        CatalogueDesignValidation.Require(expectedFiles.SetEquals(files.Keys),"Upload exactly the selected PDF output files; no additional artifacts are accepted.");
        var webPages=p.Definition.Outputs.Web?p.Definition.WebPages.Where(page=>page.IsActive).Select(page=>page with {ProductIds=page.ProductIds.Where(id=>Eligible(p,id)).ToArray()}).Where(page=>page.ProductIds.Count>0).ToArray():[];
        CatalogueDesignValidation.Require(pages.Count>0||webPages.Length>0,"The catalogue has no active pages with confirmed products for this language.");
        var productIds=pages.SelectMany(page=>page.ProductIds).Concat(webPages.SelectMany(page=>page.ProductIds)).Distinct().ToArray();
        var currentSettings=await settings.GetAsync(ct);var garments=currentSettings.GarmentTypes.ToDictionary(g=>g.Key);
        var publicProducts=productIds.Select(productId=>{var row=p.Rows.Single(r=>r.Id==productId);return PublicCatalogueService.ToPublic(p.Products[productId],row.Revision,row.UpdatedAt,p.Design.Locale,garments);}).ToArray();
        JsonElement? web=webPages.Length>0?BuildWeb(p,webPages,publicProducts):null;
        var release=new CataloguePreparedRelease(Guid.NewGuid(),p.Design.Id,p.Design.Revision,p.Definition.Slug,p.Design.Title,p.Definition.Description,p.Design.Locale,clock.UtcNow,web,publicProducts,pages,pdf,links);
        await designs.PrepareAsync(new(){Id=release.Id,DesignId=id,DesignRevision=expected,Slug=release.Slug,Locale=release.Locale,CreatedAtUtc=release.CreatedAtUtc,Fingerprint=p.Fingerprint,ProductIdsJson=CatalogueJson.Write(p.References),Json=CatalogueJson.Write(release)},assets,ct);return release;
    }
    public async Task<CatalogueReleaseSummary> PublishAsync(Guid designId,Guid releaseId,int expected,CancellationToken ct)
    {
        var record=await designs.ReleaseAsync(releaseId,ct)??throw CatalogueException.NotFound("Prepared release");
        if(record.DesignId!=designId)throw CatalogueException.NotFound("Prepared release");
        await VerifyMediaAsync(CatalogueJson.Read<CataloguePreparedRelease>(record.Json).Products.SelectMany(product=>product.Images).Select(image=>image.Sha256),ct);
        return Summary(await designs.PublishAsync(designId,releaseId,expected,clock.UtcNow,ct));
    }
    public Task WithdrawAsync(Guid designId,int expected,CancellationToken ct)=>designs.WithdrawAsync(designId,expected,clock.UtcNow,ct);
    private async Task VerifyMediaAsync(IEnumerable<string> hashes,CancellationToken ct)
    {
        foreach(var hash in hashes.Distinct(StringComparer.Ordinal))
        {
            try
            {
                var opened=await media.OpenAsync(hash,false,ct);
                if(opened is null)throw new CatalogueException("A selected catalogue image is missing. Upload it again before publishing.");
                await using var stream=opened.Value.Content;
                var actual=Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,ct));
                CatalogueDesignValidation.Require(actual==hash,"A selected catalogue image failed its integrity check. Upload it again.");
            }
            catch(IOException){throw new CatalogueException("A selected catalogue image is unavailable. Upload it again before publishing.");}
        }
    }
    public async Task<IReadOnlyList<CatalogueReleaseSummary>> ReleasesAsync(Guid id,CancellationToken ct)=>(await designs.ReleasesAsync(id,ct)).Select(Summary).ToArray();
    public async Task<object> PublicAsync(string slug,string? locale,Guid? releaseId,CancellationToken ct)
    {
        var code=locale??"uz";CatalogueDesignValidation.Require(CatalogueLocales.Supported(code),"Choose a supported catalogue language.");
        var record=await designs.PublishedAsync(slug,code,releaseId,ct)??throw CatalogueException.NotFound("Published catalogue");var release=CatalogueJson.Read<CataloguePreparedRelease>(record.Json);
        var available=(await designs.PublicationsAsync(ct)).Where(x=>x.Slug==slug).Select(x=>x.Locale).Distinct().ToArray();
        string Url(string asset)=>$"/api/public/catalogue/editions/{Uri.EscapeDataString(slug)}/assets/{Uri.EscapeDataString(asset)}?locale={Uri.EscapeDataString(code)}&release={release.Id:D}";
        return new {release.Id,release.DesignId,release.DesignRevision,release.Slug,release.Title,release.Description,release.Locale,updatedAt=release.CreatedAtUtc,publishedAtUtc=record.PublishedAtUtc,availableLocales=available,release.Web,release.Products,
            pdf=release.PdfAsset is null?null:new {pdfUrl=Url(release.PdfAsset),pages=release.Pages.Select((page,index)=>new {page.Id,number=index+1,page.Title,page.WidthMm,page.HeightMm,page.ProductIds,imageUrl=Url(page.PngAsset),pdfUrl=Url(page.PdfAsset)}).ToArray()}};
    }
    public async Task<object> PublicListAsync(string? locale,CancellationToken ct)
    {
        var code=locale??"uz";CatalogueDesignValidation.Require(CatalogueLocales.Supported(code),"Choose a supported catalogue language.");var records=await designs.PublicationsAsync(ct);
        return records.Where(r=>r.Locale==code).Select(r=>{var release=CatalogueJson.Read<CataloguePreparedRelease>(r.Json);return new {release.Id,release.Slug,release.Title,release.Description,release.Locale,updatedAt=release.CreatedAtUtc,availableLocales=records.Where(x=>x.Slug==release.Slug).Select(x=>x.Locale).Distinct().ToArray(),hasWeb=release.Web is not null,hasPdf=release.PdfAsset is not null};}).ToArray();
    }
    public async Task<CataloguePublicAsset> PublicAssetAsync(string slug,string locale,Guid releaseId,string asset,CancellationToken ct)
    {
        var record=await designs.PublishedAsync(slug,locale,releaseId,ct)??throw CatalogueException.NotFound("Published catalogue");var release=CatalogueJson.Read<CataloguePreparedRelease>(record.Json);
        if(!release.Assets.TryGetValue(asset,out var hash))throw CatalogueException.NotFound("Catalogue artifact");var stored=await designs.AssetAsync(hash,ct)??throw CatalogueException.NotFound("Catalogue artifact");
        return new(DownloadName(release,asset),stored.Sha256,stored.ContentType,stored.Bytes);
    }
    /// <summary>The file name the public reader offers: the whole edition by language, a single page by its number.</summary>
    private static string DownloadName(CataloguePreparedRelease release,string asset)
    {
        if(asset==release.PdfAsset)return $"ocharo-{release.Slug}-{release.Locale}.pdf";
        var number=release.Pages.ToList().FindIndex(page=>page.PdfAsset==asset||page.PngAsset==asset)+1;
        return $"ocharo-{release.Slug}-{number}{Path.GetExtension(asset)}";
    }
    private static IEnumerable<JsonElement> PdfPages(Preparation p)=>!p.Definition.Outputs.Pdf?[]:p.Snapshot.GetProperty("pages").EnumerateArray().Where(page=>CatalogueDesignValidation.Active(page)&&References(page,p.Products).All(id=>Eligible(p,id))&&PageImagesEligible(page,p.Products));
    private static bool Eligible(Preparation p,string id)=>p.Products.TryGetValue(id,out var product)&&ProductPublicationEligibility.Eligible(product,p.Design.Locale);
    private static IReadOnlyList<string> References(JsonElement page,IReadOnlyDictionary<string,ProductDocument> products)
    {
        var hashes=page.GetProperty("elements").EnumerateArray().Where(e=>e.TryGetProperty("src",out _)).Select(e=>CatalogueDesignValidation.ImageHash(e.GetProperty("src").GetString()!)).ToHashSet();
        var explicitIds=CatalogueDesignValidation.ReferencesOnPage(page);
        return explicitIds.Count>0?explicitIds:products.Values.Where(product=>product.Media.Any(image=>hashes.Contains(image.Sha256))).Select(product=>product.Id).Distinct().ToArray();
    }
    private static bool PageImagesEligible(JsonElement page,IReadOnlyDictionary<string,ProductDocument> products)
    {
        var references=References(page,products);
        foreach(var e in page.GetProperty("elements").EnumerateArray().Where(e=>e.TryGetProperty("src",out _)))
        {
            var hash=CatalogueDesignValidation.ImageHash(e.GetProperty("src").GetString()!);if(hash is null)continue;
            var owners=products.Values.Where(product=>product.Media.Any(image=>image.Sha256==hash)).ToArray();
            if(owners.Length==0||!owners.Any(owner=>references.Contains(owner.Id)&&ProductPublicationEligibility.ActiveImages(owner).Any(image=>image.Sha256==hash)))return false;
        }
        return true;
    }
    private static JsonElement BuildWeb(Preparation p,IReadOnlyList<CatalogueWebPage> pages,IReadOnlyList<PublicProduct> products)
    {
        var group="all";var price=CatalogueDesignValidation.PriceFields.ToDictionary(key=>key,_=>true);var copy=CatalogueDesignValidation.CopyFields.ToDictionary(key=>key,_=>"");
        if(p.Snapshot.TryGetProperty("catalogueSettings",out var settings))
        {
            group=settings.GetProperty("mediaGroup").GetString()!;foreach(var key in price.Keys.ToArray())price[key]=settings.GetProperty("priceDisplay").GetProperty(key).GetBoolean();
            if(settings.GetProperty("copyByLocale").TryGetProperty(p.Design.Locale,out var selected))foreach(var key in copy.Keys.ToArray())copy[key]=selected.GetProperty(key).GetString()!;
        }
        var media=products.ToDictionary(product=>product.Id,product=>ProductPublicationEligibility.ActiveImages(p.Products[product.Id]).Where(image=>group=="all"||ProductPublicationEligibility.MediaGroup(image)==group).Select(image=>image.Sha256).ToArray());
        return JsonSerializer.SerializeToElement(new {theme=p.Definition.Theme,pages=pages.Select(page=>new {page.Id,page.Title,page.Layout,page.ProductIds}),mediaGroup=group,priceDisplay=price,copy,productMedia=media},CatalogueJson.Options);
    }
    private static CatalogueReleaseSummary Summary(CatalogueReleaseRecord r)=>new(r.Id,r.DesignId,r.DesignRevision,r.Slug,r.Locale,r.CreatedAtUtc,r.PublishedAtUtc,r.WithdrawnAtUtc);
}
