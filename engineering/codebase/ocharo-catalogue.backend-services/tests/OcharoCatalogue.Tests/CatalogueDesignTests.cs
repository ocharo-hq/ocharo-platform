using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Persistence;
using OcharoCatalogue.Persistence.Catalogue;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Publishing.Services;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Validators;
using OcharoCatalogue.Infrastructure.Catalogue.Media.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Mediator;
using OcharoCatalogue.Api.Catalogue.Publishing.Controllers;
namespace OcharoCatalogue.Tests;

public sealed class CatalogueDesignTests
{
    private static readonly CancellationToken Ct=default;
    private static JsonElement Json(object value)=>JsonSerializer.SerializeToElement(value,CatalogueJson.Options);
    private static JsonElement Snapshot(string slug="spring",string locale="en",bool web=true,bool pdf=false,string[]? ids=null)=>Json(new {
        schemaVersion=1,kind="catalogue",templateId="catalogue-blank",locale,
        catalogue=new {slug,description="A considered collection",isActive=true,outputs=new {web,pdf},web=new {theme="editorial",pages=new[]{new {id="web-one",title="Selected pieces",layout="grid",isActive=true,productIds=ids??[]}}}},
        productRevisions=(ids??[]).ToDictionary(id=>id,_=>1),
        pages=pdf?new[]{Page("cover",[])}:Array.Empty<object>()
    });
    private static byte[] Pdf(int count,double width=210,double height=297)
    {
        using var output=new MemoryStream();using(var pdf=SKDocument.CreatePdf(output))
        {for(var i=0;i<count;i++){var canvas=pdf.BeginPage((float)(width*72/25.4),(float)(height*72/25.4));canvas.Clear(SKColors.White);pdf.EndPage();}pdf.Close();}
        return output.ToArray();
    }
    private static object Page(string id,string[] products,bool active=true)=>new {id,title=id,width=210,height=297,background="#ffffff",isActive=active,productIds=products,elements=Array.Empty<object>()};
    private static async Task<ProductRecord> Seed(CatalogueHarness h,string id="nimcha-v01",bool approved=true)
    {
        var media=await h.Get<IMediaStore>().StoreAsync(CatalogueHarness.Png((byte)(id.EndsWith("1")?81:95)),Ct);
        var locale=new ProductLocale(approved?"approved":"draft",id,"Silk",null,null);
        var doc=new ProductDocument {Id=id,GarmentType="nimcha",VariantCode=id[^3..],Status="active",Locales=new(locale,locale,locale){Additional=new Dictionary<string,ProductLocale>{{"fr",locale}}},Media=[new("image","model","model.png","image/png",media.ByteLength,media.Sha256,media.Width,media.Height,true,null,null)],Pricing=new("UZS","confirmed",[new(1,9,250000),new(10,null,190000)],null)};
        return await h.Get<ICatalogueRepository>().InsertProductAsync(ProductProjections.ToRecord(doc,1,h.Clock.GetUtcNow(),h.Clock.GetUtcNow()),"Seed",Ct);
    }
    private static async Task Mutate(CatalogueHarness h,string id,Func<ProductDocument,ProductDocument> change)
    {
        var repo=h.Get<ICatalogueRepository>();var row=(await repo.FindProductAsync(id,Ct))!;var doc=change(CatalogueJson.Read<ProductDocument>(row.Json));await repo.UpdateProductAsync(ProductProjections.ToRecord(doc,row.Revision+1,row.CreatedAt,h.Clock.GetUtcNow()),row.Revision,"Changed",Ct);
    }
    private static async Task<string> Fingerprint(TestReleases service,Guid id)=>Json(await service.PreparationAsync(id,Ct)).GetProperty("fingerprint").GetString()!;

    [Fact] public async Task Designs_preserve_revision_history_and_archive_without_touching_products()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var svc=h.Designs;var d=await svc.SaveAsync(null,new("Spring",Snapshot(ids:["nimcha-v01"])),Ct);
        var updated=await svc.SaveAsync(d.Id,new("Spring edited",d.Snapshot,d.Revision),Ct);Assert.Equal(2,updated.Revision);
        Assert.Equal("Spring",(await svc.RevisionAsync(d.Id,1,Ct)).Title);
        Assert.Equal(409,(await Assert.ThrowsAsync<CatalogueRejection>(()=>svc.SaveAsync(d.Id,new("Stale",d.Snapshot,1),Ct))).StatusCode);
        var duplicate=await svc.DuplicateAsync(d.Id,null,Ct);Assert.NotEqual(d.Id,duplicate.Id);Assert.NotEqual(d.Snapshot.GetProperty("catalogue").GetProperty("slug").GetString(),duplicate.Snapshot.GetProperty("catalogue").GetProperty("slug").GetString());
        await svc.ArchiveAsync(d.Id,2,Ct);Assert.Single(await svc.ListAsync(false,Ct));Assert.Equal(2,(await svc.ListAsync(true,Ct)).Count);
        Assert.Single(await h.Get<ICatalogueRepository>().GetProductsAsync(Ct));Assert.Single(await h.Get<ICatalogueRepository>().GetRevisionsAsync("nimcha-v01",Ct));
    }
    [Fact] public async Task Slugs_are_unique_per_language_but_can_be_reused_after_archive()
    {
        await using var h=await CatalogueHarness.CreateAsync();var s=h.Designs;var en=await s.SaveAsync(null,new("English",Snapshot()),Ct);
        Assert.Equal(409,(await Assert.ThrowsAsync<CatalogueRejection>(()=>s.SaveAsync(null,new("Duplicate",Snapshot()),Ct))).StatusCode);
        await s.SaveAsync(null,new("French",Snapshot(locale:"fr")),Ct);await s.ArchiveAsync(en.Id,1,Ct);await s.SaveAsync(null,new("Replacement",Snapshot()),Ct);Assert.Equal(2,(await s.ListAsync(false,Ct)).Count);
    }
    [Fact] public async Task Web_only_release_filters_unconfirmed_siblings_and_remains_immutable()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);await Seed(h,"nimcha-v02",false);var ds=h.Designs;var rs=h.Releases;
        var design=await ds.SaveAsync(null,new("French selection",Snapshot(locale:"fr",ids:["nimcha-v01","nimcha-v02"])),Ct);
        var prepared=await rs.PrepareAsync(design.Id,1,await Fingerprint(rs,design.Id),new Dictionary<string,byte[]>(),Ct);
        Assert.Null(prepared.PdfAsset);Assert.Empty(prepared.Pages);Assert.Single(prepared.Products);Assert.Single(prepared.Web!.Value.GetProperty("pages")[0].GetProperty("productIds").EnumerateArray());
        await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublicAsync("spring","fr",null,Ct));await rs.PublishAsync(design.Id,prepared.Id,1,Ct);
        await Mutate(h,"nimcha-v01",d=>d with {Locales=d.Locales with {Additional=new Dictionary<string,ProductLocale>{{"fr",new("draft","Changed",null,null,null)}}}});
        var released=Json(await rs.PublicAsync("spring","fr",null,Ct));Assert.Equal("nimcha-v01",released.GetProperty("products")[0].GetProperty("name").GetString());
        Assert.Equal(409,(await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublishAsync(design.Id,prepared.Id,1,Ct))).StatusCode);
        await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublicAsync("spring","de",null,Ct));
        await ds.ArchiveAsync(design.Id,1,Ct);await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublicAsync("spring","fr",prepared.Id,Ct));
    }
    [Fact] public async Task Preparation_rejects_changed_products_or_settings_after_render_starts()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var d=await h.Designs.SaveAsync(null,new("Spring",Snapshot(ids:["nimcha-v01"])),Ct);var r=h.Releases;var fingerprint=await Fingerprint(r,d.Id);
        await Mutate(h,"nimcha-v01",p=>p with {Notes="Edited after render starts"});
        Assert.Equal(409,(await Assert.ThrowsAsync<CatalogueRejection>(()=>r.PrepareAsync(d.Id,1,fingerprint,new Dictionary<string,byte[]>(),Ct))).StatusCode);
        var release=await r.PrepareAsync(d.Id,1,await Fingerprint(r,d.Id),new Dictionary<string,byte[]>(),Ct);
        await h.Settings.SaveAsync(CatalogueSettings.Default() with {PriceNote="Changed"},0,Ct);
        Assert.Equal(409,(await Assert.ThrowsAsync<CatalogueRejection>(()=>r.PublishAsync(d.Id,release.Id,1,Ct))).StatusCode);
    }
    [Fact] public async Task Pdf_pages_exclude_inactive_and_mixed_unconfirmed_products_and_enforce_exact_artifacts()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);await Seed(h,"nimcha-v02",false);
        var n=JsonNode.Parse(Snapshot(web:true,pdf:true,ids:["nimcha-v01","nimcha-v02"]).GetRawText())!;
        n["pages"]=JsonSerializer.SerializeToNode(new[]{Page("cover",[]),Page("good",["nimcha-v01"]),Page("mixed",["nimcha-v01","nimcha-v02"]),Page("inactive",["nimcha-v01"],false)},CatalogueJson.Options);
        var ds=h.Designs;var rs=h.Releases;var d=await ds.SaveAsync(null,new("PDF",Json(n)),Ct);var fingerprint=await Fingerprint(rs,d.Id);
        var files=new Dictionary<string,byte[]> { ["catalogue.pdf"]=Pdf(2) };
        foreach(var id in new[]{"cover","good"}){files[$"page-{id}.pdf"]=Pdf(1);files[$"page-{id}.png"]=CatalogueHarness.Png(81,420,594);}
        var extra=new Dictionary<string,byte[]>(files){["page-mixed.pdf"]=files["catalogue.pdf"]};await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PrepareAsync(d.Id,1,fingerprint,extra,Ct));
        var prepared=await rs.PrepareAsync(d.Id,1,fingerprint,files,Ct);Assert.Equal(new[]{"cover","good"},prepared.Pages.Select(x=>x.Id));await rs.PublishAsync(d.Id,prepared.Id,1,Ct);
        var publicView=Json(await rs.PublicAsync("spring","en",null,Ct));Assert.Contains("release=",publicView.GetProperty("pdf").GetProperty("pdfUrl").GetString());
        var downloaded=await rs.PublicAssetAsync("spring","en",prepared.Id,"catalogue.pdf",Ct);Assert.Equal(files["catalogue.pdf"],downloaded.Bytes);Assert.Equal("ocharo-spring-en.pdf",downloaded.FileName);
        Assert.Equal("ocharo-spring-2.pdf",(await rs.PublicAssetAsync("spring","en",prepared.Id,"page-good.pdf",Ct)).FileName);Assert.Equal("ocharo-spring-2.png",(await rs.PublicAssetAsync("spring","en",prepared.Id,"page-good.png",Ct)).FileName);
        await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublicAssetAsync("spring","ru",prepared.Id,"catalogue.pdf",Ct));await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublicAssetAsync("spring","en",prepared.Id,"../catalogue.pdf",Ct));
    }
    [Fact] public async Task Pdf_preparation_requires_saved_product_revisions_and_active_selected_images()
    {
        await using var h=await CatalogueHarness.CreateAsync();var row=await Seed(h);var product=CatalogueJson.Read<ProductDocument>(row.Json);var image=product.Media[0];
        var n=JsonNode.Parse(Snapshot(web:false,pdf:true,ids:[row.Id]).GetRawText())!;
        n["pages"]=JsonSerializer.SerializeToNode(new[]{Page("product",[row.Id])},CatalogueJson.Options);n["productRevisions"]![row.Id]=99;
        var ds=h.Designs;var rs=h.Releases;var d=await ds.SaveAsync(null,new("Stale PDF",Json(n)),Ct);
        Assert.Equal(409,(await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PreparationAsync(d.Id,Ct))).StatusCode);
        n["productRevisions"]![row.Id]=1;n["pages"]![0]!["elements"]=JsonSerializer.SerializeToNode(new[]{new {id="photo",label="Photo",type="image",x=0,y=0,width=100,height=100,src=$"/api/catalogue/media/{image.Sha256}",productId=row.Id}},CatalogueJson.Options);
        await ds.SaveAsync(d.Id,new("Current PDF",Json(n),1),Ct);var check=Json(await rs.PreparationAsync(d.Id,Ct));Assert.Single(check.GetProperty("pdfPageIds").EnumerateArray());
        var other=await h.Get<IMediaStore>().StoreAsync(CatalogueHarness.Png(155),Ct);
        await Mutate(h,row.Id,p=>p with {Media=[image with {IsActive=false},new("other","model","other.png","image/png",other.ByteLength,other.Sha256,other.Width,other.Height,true,null,null)]});
        n["productRevisions"]![row.Id]=2;await ds.SaveAsync(d.Id,new("Inactive selected photo",Json(n),2),Ct);
        Assert.Empty(Json(await rs.PreparationAsync(d.Id,Ct)).GetProperty("pdfPageIds").EnumerateArray());
    }
    [Fact] public async Task Additive_upgrade_preserves_existing_product_history_and_is_repeatable()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var db=h.Get<OcharoCatalogueDbContext>();
        foreach(var sql in new[]{"DROP TABLE catalogue_design_revisions","DROP TABLE catalogue_designs","DROP TABLE catalogue_publications","DROP TABLE catalogue_releases","DROP TABLE catalogue_release_assets"})await db.Database.ExecuteSqlRawAsync(sql);
        await h.Services.InitializeDatabaseAsync();await h.Services.InitializeDatabaseAsync();
        Assert.Single(await h.Get<ICatalogueRepository>().GetProductsAsync(Ct));Assert.Single(await h.Get<ICatalogueRepository>().GetRevisionsAsync("nimcha-v01",Ct));
        await h.Designs.SaveAsync(null,new("After upgrade",Snapshot()),Ct);Assert.Single(await h.Designs.ListAsync(false,Ct));
    }
    [Theory]
    [InlineData("https://example.com/image.png")]
    [InlineData("/api/organizations/other/media/image")]
    [InlineData("/templates/catalogue-original/assets/../secret.svg")]
    [InlineData("data:image/svg+xml,<svg/>")]
    public async Task Designer_rejects_remote_and_traversal_image_sources(string src)
    {
        await using var h=await CatalogueHarness.CreateAsync();var n=JsonNode.Parse(Snapshot(web:false,pdf:true).GetRawText())!;n["pages"]![0]!["elements"]=JsonSerializer.SerializeToNode(new[]{new {id="photo",label="Photo",type="image",x=0,y=0,width=100,height=100,src}},CatalogueJson.Options);
        await Assert.ThrowsAsync<CatalogueRejection>(()=>h.Designs.SaveAsync(null,new("Invalid",Json(n)),Ct));
    }
    [Fact] public async Task Explicit_withdrawal_removes_current_and_pinned_outputs_without_deleting_history()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var ds=h.Designs;var rs=h.Releases;var d=await ds.SaveAsync(null,new("Spring",Snapshot(ids:["nimcha-v01"])),Ct);
        var release=await rs.PrepareAsync(d.Id,1,await Fingerprint(rs,d.Id),new Dictionary<string,byte[]>(),Ct);await rs.PublishAsync(d.Id,release.Id,1,Ct);
        await rs.WithdrawAsync(d.Id,1,Ct);await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublicAsync("spring","en",null,Ct));await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublicAsync("spring","en",release.Id,Ct));
        Assert.NotNull((await rs.ReleasesAsync(d.Id,Ct)).Single().WithdrawnAtUtc);Assert.Equal(1,(await ds.GetAsync(d.Id,Ct)).Revision);
        Assert.Equal(409,(await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublishAsync(d.Id,release.Id,1,Ct))).StatusCode);
        var next=await rs.PrepareAsync(d.Id,1,await Fingerprint(rs,d.Id),new Dictionary<string,byte[]>(),Ct);await rs.PublishAsync(d.Id,next.Id,1,Ct);Assert.Equal(2,(await rs.ReleasesAsync(d.Id,Ct)).Count);
        Assert.Equal(next.Id,Json(await rs.PublicAsync("spring","en",null,Ct)).GetProperty("id").GetGuid());
    }
    [Fact] public async Task Pdf_parser_checks_count_geometry_and_rejects_magic_only_files()
    {
        await using var h=await CatalogueHarness.CreateAsync();var validator=h.Get<IDesignArtifactValidator>();
        validator.ValidatePdf(Pdf(2),[new(210,297),new(210,297)]);
        Assert.Throws<AppException>(()=>validator.ValidatePdf(Pdf(1),[new(210,297),new(210,297)]));
        Assert.Throws<AppException>(()=>validator.ValidatePdf(Pdf(1,100,100),[new(210,297)]));
        Assert.Throws<AppException>(()=>validator.ValidatePdf(Encoding.ASCII.GetBytes("%PDF-1.7\nnot a real PDF\n%%EOF"),[new(210,297)]));
    }
    [Fact] public async Task Unowned_and_corrupted_media_cannot_become_published_catalogue_images()
    {
        await using var h=await CatalogueHarness.CreateAsync();var unowned=await h.Get<IMediaStore>().StoreAsync(CatalogueHarness.Png(200),Ct);
        var n=JsonNode.Parse(Snapshot(web:false,pdf:true).GetRawText())!;n["pages"]![0]!["elements"]=JsonSerializer.SerializeToNode(new[]{new {id="photo",label="Photo",type="image",x=0,y=0,width=100,height=100,src=$"/api/catalogue/media/{unowned.Sha256}"}},CatalogueJson.Options);
        await Assert.ThrowsAsync<CatalogueRejection>(()=>h.Designs.SaveAsync(null,new("Unowned",Json(n)),Ct));
        var row=await Seed(h);var ds=h.Designs;var rs=h.Releases;var d=await ds.SaveAsync(null,new("Spring",Snapshot(ids:[row.Id])),Ct);
        var release=await rs.PrepareAsync(d.Id,1,await Fingerprint(rs,d.Id),new Dictionary<string,byte[]>(),Ct);
        var doc=CatalogueJson.Read<ProductDocument>(row.Json);var blob=(await h.Get<ICatalogueRepository>().FindMediaAsync(doc.Media[0].Sha256,Ct))!;
        await File.WriteAllBytesAsync(Path.Combine(h.Root,"data","blobs",blob.BlobPath),Encoding.ASCII.GetBytes("corrupted"));
        await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PreparationAsync(d.Id,Ct));await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublishAsync(d.Id,release.Id,1,Ct));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Public_media_preserves_pinned_releases_but_rejects_prepared_and_revoked_media(bool archive)
    {
        await using var h = await CatalogueHarness.CreateAsync();
        var row = await Seed(h);
        var original = CatalogueJson.Read<ProductDocument>(row.Json).Media[0];
        var designs = h.Designs;
        var releases = h.Releases;
        var mediaAccess = h.Get<MediaAccessService>();
        var design = await designs.SaveAsync(null, new("Spring", Snapshot(ids: [row.Id])), Ct);
        var first = await releases.PrepareAsync(design.Id, 1, await Fingerprint(releases, design.Id), new Dictionary<string, byte[]>(), Ct);
        await releases.PublishAsync(design.Id, first.Id, 1, Ct);
        var secondBlob = await h.Get<IMediaStore>().StoreAsync(CatalogueHarness.Png(122), Ct);
        await Mutate(h, row.Id, product => product with { Media = [original with { Sha256 = secondBlob.Sha256 }] });
        var second = await releases.PrepareAsync(design.Id, 1, await Fingerprint(releases, design.Id), new Dictionary<string, byte[]>(), Ct);
        await releases.PublishAsync(design.Id, second.Id, 1, Ct);
        var preparedBlob = await h.Get<IMediaStore>().StoreAsync(CatalogueHarness.Png(133), Ct);
        await Mutate(h, row.Id, product => product with { Media = [original with { Sha256 = preparedBlob.Sha256 }] });
        await releases.PrepareAsync(design.Id, 1, await Fingerprint(releases, design.Id), new Dictionary<string, byte[]>(), Ct);
        await Mutate(h, row.Id, product => product with { Status = ProductStatus.Draft });
        Assert.True(await mediaAccess.IsPublicAsync(original.Sha256, Ct));
        Assert.True(await mediaAccess.IsPublicAsync(secondBlob.Sha256, Ct));
        Assert.False(await mediaAccess.IsPublicAsync(preparedBlob.Sha256, Ct));
        if (archive) await designs.ArchiveAsync(design.Id, 1, Ct);
        else await releases.WithdrawAsync(design.Id, 1, Ct);
        Assert.False(await mediaAccess.IsPublicAsync(original.Sha256, Ct));
        Assert.False(await mediaAccess.IsPublicAsync(secondBlob.Sha256, Ct));
    }
    [Fact] public async Task Prepared_web_preserves_copy_media_group_and_all_price_flags()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var n=JsonNode.Parse(Snapshot(ids:["nimcha-v01"]).GetRawText())!;
        n["catalogueSettings"]=JsonSerializer.SerializeToNode(new {mediaGroup="model",priceDisplay=new {show=true,showCurrency=false,showQuantity=true,showRetail=false,showWholesale=true},copyByLocale=new Dictionary<string,object>{{"en",CatalogueDesignValidator.CopyFields.ToDictionary(key=>key,key=>key=="header"?"Spring atelier":key)}}},CatalogueJson.Options);
        var d=await h.Designs.SaveAsync(null,new("Spring",Json(n)),Ct);var rs=h.Releases;var r=await rs.PrepareAsync(d.Id,1,await Fingerprint(rs,d.Id),new Dictionary<string,byte[]>(),Ct);
        Assert.Equal("Spring atelier",r.Web!.Value.GetProperty("copy").GetProperty("header").GetString());Assert.False(r.Web.Value.GetProperty("priceDisplay").GetProperty("showRetail").GetBoolean());Assert.Single(r.Web.Value.GetProperty("productMedia").GetProperty("nimcha-v01").EnumerateArray());
    }
    [Fact] public async Task Release_artifacts_on_files_land_there_once_and_the_public_route_redirects_while_published()
    {
        var files=new TestFiles();await using var h=await CatalogueHarness.CreateAsync(files:files);await Seed(h);
        var n=JsonNode.Parse(Snapshot(web:true,pdf:true,ids:["nimcha-v01"]).GetRawText())!;
        n["pages"]=JsonSerializer.SerializeToNode(new[]{Page("cover",[]),Page("good",["nimcha-v01"])},CatalogueJson.Options);
        var ds=h.Designs;var rs=h.Releases;var designs=h.Get<IDesignRepository>();var d=await ds.SaveAsync(null,new("PDF",Json(n)),Ct);var fingerprint=await Fingerprint(rs,d.Id);
        var upload=new Dictionary<string,byte[]>{["catalogue.pdf"]=Pdf(2),["page-cover.pdf"]=Pdf(1),["page-good.pdf"]=Pdf(1),["page-cover.png"]=CatalogueHarness.Png(83,420,594),["page-good.png"]=CatalogueHarness.Png(84,420,594)};
        var imagesBefore=files.Uploads;var prepared=await rs.PrepareAsync(d.Id,1,fingerprint,upload,Ct);var uploaded=files.Uploads-imagesBefore;
        await rs.PrepareAsync(d.Id,1,fingerprint,upload,Ct);Assert.Equal(imagesBefore+uploaded,files.Uploads);
        Assert.Equal(prepared.Assets.Values.Distinct().Count(),uploaded);
        await rs.PublishAsync(d.Id,prepared.Id,1,Ct);
        var pdfSha=prepared.Assets["catalogue.pdf"];var record=await designs.ReleaseFileAsync(pdfSha,Ct);
        Assert.NotNull(record);Assert.Null(await designs.AssetAsync(pdfSha,Ct));Assert.Empty(await designs.AssetsWithoutFileAsync(Ct));
        var model=files.Model(record!.FileId);Assert.Equal("ocharo-spring-en.pdf",model.Name);Assert.Equal("catalogue",model.AppId);Assert.Equal("application/pdf",model.ContentType);
        Assert.Contains(new FileReference("catalogue.release",pdfSha),files.ReferencesOf(model.Id));
        var served=await rs.PublicAssetAsync("spring","en",prepared.Id,"catalogue.pdf",Ct);
        Assert.Null(served.Bytes);Assert.Equal(TestFiles.Link(model.Id,null,FileDisposition.Attachment),served.Location);Assert.Equal("ocharo-spring-en.pdf",served.FileName);
        var controller=new PublishedDesignsController(h.Get<ISender>()){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        var redirect=Assert.IsType<RedirectResult>(await controller.GetAsset("spring","page-good.png","en",prepared.Id,Ct));
        var png=files.Model((await designs.ReleaseFileAsync(prepared.Assets["page-good.png"],Ct))!.FileId);
        Assert.Equal(TestFiles.Link(png.Id,null,FileDisposition.Attachment).AbsoluteUri,redirect.Url);Assert.Equal("ocharo-spring-2.png",png.Name);
        Assert.Equal("no-store",controller.Response.Headers.CacheControl.ToString());
        await rs.WithdrawAsync(d.Id,1,Ct);await Assert.ThrowsAsync<CatalogueRejection>(()=>rs.PublicAssetAsync("spring","en",prepared.Id,"catalogue.pdf",Ct));
    }
}
