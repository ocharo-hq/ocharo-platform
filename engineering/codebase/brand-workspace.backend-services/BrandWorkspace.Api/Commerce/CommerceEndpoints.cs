using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Preparation;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Commerce;
using BrandWorkspace.Persistence.Commerce;
using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Web.SecureHeaders;

namespace BrandWorkspace.Api.Commerce;

public sealed record CatalogueEditionUpload(int ExpectedRevision,Guid SourceDesignId,int SourceRevision);
public sealed record CommerceSave(int ExpectedRevision,CommerceSnapshot Snapshot);
public sealed record ReleaseRequest(int ExpectedRevision,string Note);
public sealed record WithdrawRequest(string Reason);
public sealed record AdoptionRequest(string Slug);
public sealed record AttachMediaRequest(int ExpectedRevision,Guid MediaId,MediaApproval Provenance);

public static class CommerceEndpoints
{
    private static IResult CommerceOk(object value) => Results.Json(value, CommerceJson.Options);
    private static void Editor(HttpContext h) { if(!OrganizationAccessFilter.GetAccess(h).CanEdit)throw new PreparationException("forbidden","Editor access is required."); }
    private static void Owner(HttpContext h) { if(!OrganizationAccessFilter.GetAccess(h).CanApprove)throw new PreparationException("forbidden","Owner approval is required."); }
    public static WebApplication MapCommerceEndpoints(this WebApplication app)
    {
        var scoped=app.MapGroup("/api/organizations/{organizationId:guid}").RequireAuthorization()
            .AddEndpointFilter<OrganizationAccessFilter>().AddEndpointFilter<PreparationErrorFilter>();
        scoped.MapGet("/brands/{brandId:guid}/commerce",async(Guid organizationId,Guid brandId,ICommerceRepository repository,CancellationToken token)=>CommerceOk(new {data=await repository.GetAsync(organizationId,brandId,token)}));
        scoped.MapPut("/brands/{brandId:guid}/commerce",async(Guid organizationId,Guid brandId,CommerceSave request,HttpContext h,ICommerceRepository repository,CancellationToken token)=>{
            Editor(h);var a=OrganizationAccessFilter.GetAccess(h);return CommerceOk(new {data=await repository.SaveAsync(organizationId,a.UserId,brandId,request.ExpectedRevision,request.Snapshot,[],a.CanApprove,token)});
        });
        scoped.MapPost("/brands/{brandId:guid}/commerce/adopt-preserved",async(Guid organizationId,Guid brandId,AdoptionRequest request,HttpContext h,ICommerceRepository repository,LegacyCommerceReader reader,CommerceMediaFiles media,IConfiguration config,IWebHostEnvironment env,CancellationToken token)=>{
            Owner(h);
            // Global preserved data may only enter the explicitly bound organization. Check before donor access.
            if(!Guid.TryParse(config["Commerce:AdoptionOrganizationId"],out var adoptionOrganizationId) || adoptionOrganizationId==Guid.Empty || adoptionOrganizationId!=organizationId)
                throw new PreparationException("forbidden","Preserved source adoption is not enabled for this organization.");
            if(await repository.GetAsync(organizationId,brandId,token) is not null)throw new PreparationException("conflict","Adoption is only allowed into an empty brand draft.");
            var root=config["Commerce:SourceRoot"];
            if(string.IsNullOrWhiteSpace(root)){if(!env.IsLocalRun(config))throw new PreparationException("validation","Preserved source storage is not configured.");root=Path.GetFullPath(Path.Combine(env.ContentRootPath,"../.."));}
            var imported=await reader.ReadAsync(root,request.Slug,token);
            var blobs=await media.StoreAsync(brandId,imported.Blobs,MaximumImageBytes,null,token);
            return CommerceOk(new {data=await repository.SaveAsync(organizationId,OrganizationAccessFilter.GetAccess(h).UserId,brandId,0,imported.Snapshot,blobs,true,token,preserveImportedPrices:true)});
        });
        // With WoW2 Files the brand references the preparation image's file instead of copying its bytes (files-adoption #11).
        scoped.MapPost("/brands/{brandId:guid}/commerce/media",async(Guid organizationId,Guid brandId,AttachMediaRequest request,HttpContext h,ICommerceRepository repository,CommerceMediaFiles media,CancellationToken token)=>{
            Editor(h);var a=OrganizationAccessFilter.GetAccess(h);var draft=await repository.GetAsync(organizationId,brandId,token)??throw new PreparationException("not-found","Save a brand draft first.");
            var blob=await media.FromPreparationAsync(organizationId,brandId,request.MediaId,token);
            var approvals=draft.Snapshot.MediaApprovals.ToDictionary(x=>x.Key,x=>x.Value);approvals[blob.Sha256]=request.Provenance;
            var saved=await repository.SaveAsync(organizationId,a.UserId,brandId,request.ExpectedRevision,draft.Snapshot with {MediaApprovals=approvals},[blob],a.CanApprove,token);
            return CommerceOk(new {data=saved});
        });
        // Bytes, never a redirect: the design export fetch()es this route into a blob, and Files' /f links send no CORS header.
        scoped.MapGet("/brands/{brandId:guid}/commerce/media/{sha256}",async(Guid organizationId,Guid brandId,string sha256,HttpContext h,ICommerceRepository repository,CommerceMediaFiles files,CancellationToken token)=>{
            h.Response.Headers.CacheControl="no-store";
            h.Response.Headers.XContentTypeOptions="nosniff";
            if(!CommerceRules.Hash(sha256))return Results.NotFound();
            // Resolve the brand through the organization before reading its private content-addressed bytes.
            if(await repository.GetAsync(organizationId,brandId,token) is null)return Results.NotFound();
            var media=await files.ReadAsync(brandId,sha256,token);
            return media is null?Results.NotFound():Results.File(media.Bytes!,media.ContentType);
        });
        scoped.MapPost("/brands/{brandId:guid}/commerce/catalogue-editions",async(Guid organizationId,Guid brandId,HttpContext h,ICommerceRepository repository,IDesignDocumentRepository designs,CommerceMediaFiles media,TimeProvider time,CancellationToken token)=>{
            Editor(h);
            var metadataAndFiles=await ReadEditionForm(h,token);var metadata=metadataAndFiles.Metadata;
            var draft=await repository.GetAsync(organizationId,brandId,token)??throw new PreparationException("not-found","Save the brand draft first.");
            if(draft.Revision!=metadata.ExpectedRevision)throw new PreparationException("conflict","The draft changed. Reload before attaching the edition.");
            var design=await designs.GetDocumentAsync(organizationId,metadata.SourceDesignId,metadata.SourceRevision,token);
            var built=CatalogueEditionBuilder.Build(design,brandId,metadataAndFiles.Files,time.GetUtcNow(),draft.Snapshot);
            // The artifacts land in Files before the attach's transaction, named as the public download saves them.
            var blobs=await media.StoreAsync(brandId,built.Blobs,CatalogueEditionBuilder.MaximumPdfBytes,EditionNames(draft.Snapshot.Slug,built.Edition),token);
            var saved=await repository.AttachCatalogueEditionAsync(organizationId,OrganizationAccessFilter.GetAccess(h).UserId,brandId,metadata.ExpectedRevision,built.Edition,blobs,token);
            return CommerceOk(new{data=saved});
        });
        scoped.MapPost("/brands/{brandId:guid}/commerce/catalogue-editions/{editionId:guid}/publish",async(Guid organizationId,Guid brandId,Guid editionId,ReleaseRequest request,HttpContext h,ICommerceRepository repository,CancellationToken token)=>{
            Owner(h);return CommerceOk(new{data=await repository.PublishCatalogueEditionByIdAsync(organizationId,OrganizationAccessFilter.GetAccess(h).UserId,brandId,request.ExpectedRevision,editionId,request.Note,token)});
        });
        scoped.MapPost("/brands/{brandId:guid}/commerce/catalogue-editions/{locale}/publish",async(Guid organizationId,Guid brandId,string locale,ReleaseRequest request,HttpContext h,ICommerceRepository repository,CancellationToken token)=>{
            Owner(h);CommerceRules.Require(CatalogueLanguages.Supported(locale),"Choose a supported catalogue language.");
            return CommerceOk(new{data=await repository.PublishCatalogueEditionAsync(organizationId,OrganizationAccessFilter.GetAccess(h).UserId,brandId,request.ExpectedRevision,locale,request.Note,token)});
        });
        scoped.MapGet("/brands/{brandId:guid}/commerce/releases",async(Guid organizationId,Guid brandId,ICommerceRepository repository,CancellationToken token)=>CommerceOk(new {data=await repository.ReleasesAsync(organizationId,brandId,token)}));
        scoped.MapPost("/brands/{brandId:guid}/commerce/releases",async(Guid organizationId,Guid brandId,ReleaseRequest request,HttpContext h,ICommerceRepository repository,CancellationToken token)=>{
            Owner(h);return CommerceOk(new {data=await repository.PublishAsync(organizationId,OrganizationAccessFilter.GetAccess(h).UserId,brandId,request.ExpectedRevision,request.Note,token)});
        });
        scoped.MapPost("/brands/{brandId:guid}/commerce/releases/{releaseId:guid}/withdraw",async(Guid organizationId,Guid brandId,Guid releaseId,WithdrawRequest request,HttpContext h,ICommerceRepository repository,CancellationToken token)=>{
            Owner(h);await repository.WithdrawAsync(organizationId,OrganizationAccessFilter.GetAccess(h).UserId,brandId,releaseId,request.Reason,token);return Results.NoContent();
        });
        scoped.MapGet("/brands/{brandId:guid}/creative-imports",async(Guid organizationId,Guid brandId,ICommerceRepository repository,CancellationToken token)=>CommerceOk(new {data=await repository.CreativeImportsAsync(organizationId,brandId,token)}));
        scoped.MapPost("/creative-imports",async(Guid organizationId,CreativeImportInput request,HttpContext h,ICommerceRepository repository,IPreparationRepository preparation,CancellationToken token)=>{
            Editor(h);
            CommerceRules.ValidateCreativeInput(request);
            var media=await preparation.GetMediaFileAsync(organizationId,request.Derivative.MediaId,token);
            CommerceRules.Require(media.Asset.Sha256==request.Derivative.Sha256&&media.Asset.ContentType==request.Derivative.MediaType,"Derivative bytes do not match the submitted manifest.");
            if(request.ProductId is not null){var draft=await repository.GetAsync(organizationId,request.BrandId,token);CommerceRules.Require(draft?.Snapshot.Products.Any(p=>p.Document.Id==request.ProductId)==true,"Select a product in this brand.");}
            return CommerceOk(new {data=await repository.ImportCreativeAsync(organizationId,OrganizationAccessFilter.GetAccess(h).UserId,request,token)});
        });
        // Other origins embed these: Blog's catalogue pages show edition page images and link the PDFs, and published product
        // media load in Marketing and Blog, so the routes and their Files redirects answer Cross-Origin-Resource-Policy: cross-origin.
        var publicApi=app.MapGroup("/api/public/v1/brands/{slug}").AllowAnonymous().AllowCrossOriginEmbedding().AddEndpointFilter<PreparationErrorFilter>();
        publicApi.MapGet("",async(string slug,Guid? release,ICommerceRepository repository,CancellationToken token)=>{
            var value=await repository.PublicAsync(slug,release,token);return value is null?Results.NotFound():CommerceOk(PublicProjection.Brand(value));
        });
        publicApi.MapGet("/catalogue",async(string slug,string? locale,Guid? release,ICommerceRepository repository,CancellationToken token)=>{
            var value=await repository.PublicAsync(slug,release,token);return value is null?Results.NotFound():CommerceOk(PublicProjection.Catalogue(value,locale??"uz"));
        });
        // The release check decides each time and the answer stays no-store; with WoW2 Files it redirects to a link.
        publicApi.MapGet("/catalogue-editions/{editionId:guid}/{asset}",async(string slug,Guid editionId,string asset,Guid? release,string? download,HttpContext h,ICommerceRepository repository,CommerceMediaFiles files,CancellationToken token)=>{
            h.Response.Headers.XContentTypeOptions="nosniff";h.Response.Headers.CacheControl="no-store";
            if(release is null)return Results.NotFound();
            var published=await repository.PublicAsync(slug,release,token);
            var edition=published?.Snapshot.CatalogueEditions.SingleOrDefault(e=>e.Id==editionId && e.ApprovedForPublication);
            if(published is null || edition is null || !CommerceRules.PdfEligible(edition,published.Snapshot))return Results.NotFound();
            string? hash=null;string? contentType=null;
            if(asset=="catalogue.pdf"){hash=edition.PdfSha256;contentType="application/pdf";}
            else foreach(var page in edition.Pages)
            {
                if(asset==$"{page.Id}.png"){hash=page.PngSha256;contentType="image/png";break;}
                if(asset==$"{page.Id}.pdf"){hash=page.PdfSha256;contentType="application/pdf";break;}
            }
            if(hash is null)return Results.NotFound();
            var blob=await files.OpenAsync(published.BrandId,hash,download=="1",token);
            if(blob is null || blob.ContentType!=contentType)return Results.NotFound();
            if(blob.Location is {} location)return Results.Redirect(location.AbsoluteUri);
            return Results.File(blob.Bytes!,contentType!,download=="1"?$"{published.Snapshot.Slug}-{edition.Locale}-{asset}":null);
        });
        publicApi.MapGet("/media/{sha256}",async(string slug,string sha256,Guid? release,HttpContext h,ICommerceRepository repository,CommerceMediaFiles files,CancellationToken token)=>{
            var value=await repository.PublicAsync(slug,release,token);
            if(value is null||!CommerceRules.ApprovedHashes(value.Snapshot).Contains(sha256))return Results.NotFound();
            var media=await files.OpenAsync(value.BrandId,sha256,false,token);if(media is null)return Results.NotFound();
            h.Response.Headers.XContentTypeOptions="nosniff";h.Response.Headers.CacheControl="no-store";
            return media.Location is {} location?Results.Redirect(location.AbsoluteUri):Results.File(media.Bytes!,media.ContentType);
        });
        return app;
    }
    /// <summary>The largest brand image the adoption and attach flows store.</summary>
    private const int MaximumImageBytes=15*1024*1024;
    /// <summary>Names each edition artifact as the public download saves it: <c>{slug}-{locale}-{asset}</c>.</summary>
    private static IReadOnlyDictionary<string,string> EditionNames(string slug,CatalogueEdition edition)
    {
        var names=new Dictionary<string,string>(StringComparer.Ordinal);
        if(edition.PdfSha256 is {} pdf)names.TryAdd(pdf,$"{slug}-{edition.Locale}-catalogue.pdf");
        foreach(var page in edition.Pages){names.TryAdd(page.PngSha256,$"{slug}-{edition.Locale}-{page.Id}.png");names.TryAdd(page.PdfSha256,$"{slug}-{edition.Locale}-{page.Id}.pdf");}
        return names;
    }
    private static async Task<(CatalogueEditionUpload Metadata,IReadOnlyDictionary<string,byte[]> Files)> ReadEditionForm(HttpContext h,CancellationToken token)
    {
        const int requestLimit=CatalogueEditionBuilder.MaximumTotalBytes+1024*1024;
        if(h.Request.ContentLength>requestLimit)throw new PreparationException("too-large","Catalogue upload exceeds 256 MiB.");
        if(h.Features.Get<IHttpMaxRequestBodySizeFeature>() is {IsReadOnly:false} limit)limit.MaxRequestBodySize=requestLimit;
        CommerceRules.Require(h.Request.HasFormContentType,"Send catalogue artifacts as multipart form data.");
        IFormCollection form;
        try{form=await h.Request.ReadFormAsync(new FormOptions{MultipartBodyLengthLimit=CatalogueEditionBuilder.MaximumPdfBytes,MultipartHeadersLengthLimit=8192,MultipartBoundaryLengthLimit=128,ValueCountLimit=202,ValueLengthLimit=2000},token);}
        catch(InvalidDataException){throw new PreparationException("validation","The catalogue form is malformed or exceeds its limits.");}
        catch(IOException)when(!token.IsCancellationRequested&&!h.RequestAborted.IsCancellationRequested){throw new PreparationException("validation","The catalogue form ended before its final boundary.");}
        CommerceRules.Require(form.Count==1 && form["metadata"].Count==1 && form.Files.Count<=201,"Send metadata and the exact catalogue file set.");
        CatalogueEditionUpload metadata;
        try {
            using var json=JsonDocument.Parse(form["metadata"].ToString());var value=json.RootElement;
            CommerceRules.Require(value.ValueKind==JsonValueKind.Object && value.EnumerateObject().Count()==3 && value.EnumerateObject().Select(p=>p.Name).ToHashSet().SetEquals(new[]{"expectedRevision","sourceDesignId","sourceRevision"}),"Catalogue metadata has unknown or duplicate fields.");
            metadata=JsonSerializer.Deserialize<CatalogueEditionUpload>(value.GetRawText(),CommerceJson.Options)??throw new JsonException();
        }catch(JsonException){throw new PreparationException("validation","Catalogue metadata is invalid.");}
        CommerceRules.Require(metadata.ExpectedRevision>0 && metadata.SourceRevision>0 && metadata.SourceDesignId!=Guid.Empty,"Saved source and draft revisions are required.");
        CommerceRules.Require(form.Files.Sum(f=>f.Length)<=CatalogueEditionBuilder.MaximumTotalBytes,"Catalogue artifacts exceed 256 MiB.");
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var file in form.Files)
        {
            CommerceRules.Require(file.Name.Length<=120 && !files.ContainsKey(file.Name) && file.Length>0 && file.Length<=(file.Name=="pdf"?CatalogueEditionBuilder.MaximumPdfBytes:CatalogueEditionBuilder.MaximumPageBytes),"Catalogue files must be distinct and within their size limits.");
            using var buffer=new MemoryStream();await file.CopyToAsync(buffer,token);files.Add(file.Name,buffer.ToArray());
        }
        return(metadata,files);
    }
}
