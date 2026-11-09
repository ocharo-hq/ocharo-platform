using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
namespace OcharoCatalogue.Tests.Integration;

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

    [Fact] public async Task Design_ShouldPreserveRevisionHistoryAndArchiveWithoutTouchingProducts_WhenSavedAndArchived()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var svc=h.Designs;var d=await svc.SaveAsync(null,new("Spring",Snapshot(ids:["nimcha-v01"])),Ct);
        var updated=await svc.SaveAsync(d.Id,new("Spring edited",d.Snapshot,d.Revision),Ct);updated.Revision.Should().Be(2);
        (await svc.RevisionAsync(d.Id,1,Ct)).Title.Should().Be("Spring");
        (await FluentActions.Awaiting(()=>svc.SaveAsync(d.Id,new("Stale",d.Snapshot,1),Ct)).Should().ThrowAsync<CatalogueRejection>()).Which.StatusCode.Should().Be(409);
        var duplicate=await svc.DuplicateAsync(d.Id,null,Ct);duplicate.Id.Should().NotBe(d.Id);duplicate.Snapshot.GetProperty("catalogue").GetProperty("slug").GetString().Should().NotBe(d.Snapshot.GetProperty("catalogue").GetProperty("slug").GetString());
        await svc.ArchiveAsync(d.Id,2,Ct);(await svc.ListAsync(false,Ct)).Should().ContainSingle();(await svc.ListAsync(true,Ct)).Count.Should().Be(2);
        (await h.Get<ICatalogueRepository>().GetProductsAsync(Ct)).Should().ContainSingle();(await h.Get<ICatalogueRepository>().GetRevisionsAsync("nimcha-v01",Ct)).Should().ContainSingle();
    }
    [Fact] public async Task Slug_ShouldBeUniquePerLanguageAndReusableAfterArchive_WhenDesignsAreSaved()
    {
        await using var h=await CatalogueHarness.CreateAsync();var s=h.Designs;var en=await s.SaveAsync(null,new("English",Snapshot()),Ct);
        (await FluentActions.Awaiting(()=>s.SaveAsync(null,new("Duplicate",Snapshot()),Ct)).Should().ThrowAsync<CatalogueRejection>()).Which.StatusCode.Should().Be(409);
        await s.SaveAsync(null,new("French",Snapshot(locale:"fr")),Ct);await s.ArchiveAsync(en.Id,1,Ct);await s.SaveAsync(null,new("Replacement",Snapshot()),Ct);(await s.ListAsync(false,Ct)).Count.Should().Be(2);
    }
    [Fact] public async Task WebRelease_ShouldFilterUnconfirmedSiblingsAndStayImmutable_WhenPublished()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);await Seed(h,"nimcha-v02",false);var ds=h.Designs;var rs=h.Releases;
        var design=await ds.SaveAsync(null,new("French selection",Snapshot(locale:"fr",ids:["nimcha-v01","nimcha-v02"])),Ct);
        var prepared=await rs.PrepareAsync(design.Id,1,await Fingerprint(rs,design.Id),new Dictionary<string,byte[]>(),Ct);
        prepared.PdfAsset.Should().BeNull();prepared.Pages.Should().BeEmpty();prepared.Products.Should().ContainSingle();prepared.Web!.Value.GetProperty("pages")[0].GetProperty("productIds").EnumerateArray().Should().ContainSingle();
        await FluentActions.Awaiting(()=>rs.PublicAsync("spring","fr",null,Ct)).Should().ThrowAsync<CatalogueRejection>();await rs.PublishAsync(design.Id,prepared.Id,1,Ct);
        await Mutate(h,"nimcha-v01",d=>d with {Locales=d.Locales with {Additional=new Dictionary<string,ProductLocale>{{"fr",new("draft","Changed",null,null,null)}}}});
        var released=Json(await rs.PublicAsync("spring","fr",null,Ct));released.GetProperty("products")[0].GetProperty("name").GetString().Should().Be("nimcha-v01");
        (await FluentActions.Awaiting(()=>rs.PublishAsync(design.Id,prepared.Id,1,Ct)).Should().ThrowAsync<CatalogueRejection>()).Which.StatusCode.Should().Be(409);
        await FluentActions.Awaiting(()=>rs.PublicAsync("spring","de",null,Ct)).Should().ThrowAsync<CatalogueRejection>();
        await ds.ArchiveAsync(design.Id,1,Ct);await FluentActions.Awaiting(()=>rs.PublicAsync("spring","fr",prepared.Id,Ct)).Should().ThrowAsync<CatalogueRejection>();
    }
    [Fact] public async Task Preparation_ShouldRejectChangedProductsOrSettings_WhenRenderHasStarted()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var d=await h.Designs.SaveAsync(null,new("Spring",Snapshot(ids:["nimcha-v01"])),Ct);var r=h.Releases;var fingerprint=await Fingerprint(r,d.Id);
        await Mutate(h,"nimcha-v01",p=>p with {Notes="Edited after render starts"});
        (await FluentActions.Awaiting(()=>r.PrepareAsync(d.Id,1,fingerprint,new Dictionary<string,byte[]>(),Ct)).Should().ThrowAsync<CatalogueRejection>()).Which.StatusCode.Should().Be(409);
        var release=await r.PrepareAsync(d.Id,1,await Fingerprint(r,d.Id),new Dictionary<string,byte[]>(),Ct);
        await h.Settings.SaveAsync(CatalogueSettings.Default() with {PriceNote="Changed"},0,Ct);
        (await FluentActions.Awaiting(()=>r.PublishAsync(d.Id,release.Id,1,Ct)).Should().ThrowAsync<CatalogueRejection>()).Which.StatusCode.Should().Be(409);
    }
    [Fact] public async Task PdfPages_ShouldExcludeInactiveAndMixedUnconfirmedProductsAndEnforceExactArtifacts_WhenPrepared()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);await Seed(h,"nimcha-v02",false);
        var n=JsonNode.Parse(Snapshot(web:true,pdf:true,ids:["nimcha-v01","nimcha-v02"]).GetRawText())!;
        n["pages"]=JsonSerializer.SerializeToNode(new[]{Page("cover",[]),Page("good",["nimcha-v01"]),Page("mixed",["nimcha-v01","nimcha-v02"]),Page("inactive",["nimcha-v01"],false)},CatalogueJson.Options);
        var ds=h.Designs;var rs=h.Releases;var d=await ds.SaveAsync(null,new("PDF",Json(n)),Ct);var fingerprint=await Fingerprint(rs,d.Id);
        var files=new Dictionary<string,byte[]> { ["catalogue.pdf"]=Pdf(2) };
        foreach(var id in new[]{"cover","good"}){files[$"page-{id}.pdf"]=Pdf(1);files[$"page-{id}.png"]=CatalogueHarness.Png(81,420,594);}
        var extra=new Dictionary<string,byte[]>(files){["page-mixed.pdf"]=files["catalogue.pdf"]};await FluentActions.Awaiting(()=>rs.PrepareAsync(d.Id,1,fingerprint,extra,Ct)).Should().ThrowAsync<CatalogueRejection>();
        var prepared=await rs.PrepareAsync(d.Id,1,fingerprint,files,Ct);prepared.Pages.Select(x=>x.Id).Should().BeEquivalentTo(new[]{"cover","good"}, options => options.WithStrictOrdering());await rs.PublishAsync(d.Id,prepared.Id,1,Ct);
        var publicView=Json(await rs.PublicAsync("spring","en",null,Ct));publicView.GetProperty("pdf").GetProperty("pdfUrl").GetString().Should().Contain("release=");
        var downloaded=await rs.PublicAssetAsync("spring","en",prepared.Id,"catalogue.pdf",Ct);downloaded.Bytes.Should().Equal(files["catalogue.pdf"]);downloaded.FileName.Should().Be("ocharo-spring-en.pdf");
        (await rs.PublicAssetAsync("spring","en",prepared.Id,"page-good.pdf",Ct)).FileName.Should().Be("ocharo-spring-2.pdf");(await rs.PublicAssetAsync("spring","en",prepared.Id,"page-good.png",Ct)).FileName.Should().Be("ocharo-spring-2.png");
        await FluentActions.Awaiting(()=>rs.PublicAssetAsync("spring","ru",prepared.Id,"catalogue.pdf",Ct)).Should().ThrowAsync<CatalogueRejection>();await FluentActions.Awaiting(()=>rs.PublicAssetAsync("spring","en",prepared.Id,"../catalogue.pdf",Ct)).Should().ThrowAsync<CatalogueRejection>();
    }
    [Fact] public async Task PdfPreparation_ShouldRequireSavedProductRevisionsAndActiveSelectedImages_WhenPrepared()
    {
        await using var h=await CatalogueHarness.CreateAsync();var row=await Seed(h);var product=CatalogueJson.Read<ProductDocument>(row.Json);var image=product.Media[0];
        var n=JsonNode.Parse(Snapshot(web:false,pdf:true,ids:[row.Id]).GetRawText())!;
        n["pages"]=JsonSerializer.SerializeToNode(new[]{Page("product",[row.Id])},CatalogueJson.Options);n["productRevisions"]![row.Id]=99;
        var ds=h.Designs;var rs=h.Releases;var d=await ds.SaveAsync(null,new("Stale PDF",Json(n)),Ct);
        (await FluentActions.Awaiting(()=>rs.PreparationAsync(d.Id,Ct)).Should().ThrowAsync<CatalogueRejection>()).Which.StatusCode.Should().Be(409);
        n["productRevisions"]![row.Id]=1;n["pages"]![0]!["elements"]=JsonSerializer.SerializeToNode(new[]{new {id="photo",label="Photo",type="image",x=0,y=0,width=100,height=100,src=$"/api/catalogue/media/{image.Sha256}",productId=row.Id}},CatalogueJson.Options);
        await ds.SaveAsync(d.Id,new("Current PDF",Json(n),1),Ct);var check=Json(await rs.PreparationAsync(d.Id,Ct));check.GetProperty("pdfPageIds").EnumerateArray().Should().ContainSingle();
        var other=await h.Get<IMediaStore>().StoreAsync(CatalogueHarness.Png(155),Ct);
        await Mutate(h,row.Id,p=>p with {Media=[image with {IsActive=false},new("other","model","other.png","image/png",other.ByteLength,other.Sha256,other.Width,other.Height,true,null,null)]});
        n["productRevisions"]![row.Id]=2;await ds.SaveAsync(d.Id,new("Inactive selected photo",Json(n),2),Ct);
        Json(await rs.PreparationAsync(d.Id,Ct)).GetProperty("pdfPageIds").EnumerateArray().Should().BeEmpty();
    }
    [Fact] public async Task MigrationSet_ShouldPreserveProductHistoryAndBeRepeatable_WhenAppliedAgain()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var database=h.Get<IConfiguration>()[CatalogueTestDatabase.ConnectionKey]!;
        await CatalogueTestDatabase.MigrateAsync(database);await CatalogueTestDatabase.MigrateAsync(database);
        (await h.Get<ICatalogueRepository>().GetProductsAsync(Ct)).Should().ContainSingle();(await h.Get<ICatalogueRepository>().GetRevisionsAsync("nimcha-v01",Ct)).Should().ContainSingle();
        await h.Designs.SaveAsync(null,new("After upgrade",Snapshot()),Ct);(await h.Designs.ListAsync(false,Ct)).Should().ContainSingle();
    }
    [Theory]
    [InlineData("https://example.com/image.png")]
    [InlineData("/api/organizations/other/media/image")]
    [InlineData("/templates/catalogue-original/assets/../secret.svg")]
    [InlineData("data:image/svg+xml,<svg/>")]
    public async Task Designer_ShouldRejectRemoteAndTraversalImageSources_WhenSourceIsUnsafe(string src)
    {
        await using var h=await CatalogueHarness.CreateAsync();var n=JsonNode.Parse(Snapshot(web:false,pdf:true).GetRawText())!;n["pages"]![0]!["elements"]=JsonSerializer.SerializeToNode(new[]{new {id="photo",label="Photo",type="image",x=0,y=0,width=100,height=100,src}},CatalogueJson.Options);
        await FluentActions.Awaiting(()=>h.Designs.SaveAsync(null,new("Invalid",Json(n)),Ct)).Should().ThrowAsync<CatalogueRejection>();
    }
    [Fact] public async Task Withdrawal_ShouldRemoveCurrentAndPinnedOutputsWithoutDeletingHistory_WhenExplicit()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var ds=h.Designs;var rs=h.Releases;var d=await ds.SaveAsync(null,new("Spring",Snapshot(ids:["nimcha-v01"])),Ct);
        var release=await rs.PrepareAsync(d.Id,1,await Fingerprint(rs,d.Id),new Dictionary<string,byte[]>(),Ct);await rs.PublishAsync(d.Id,release.Id,1,Ct);
        await rs.WithdrawAsync(d.Id,1,Ct);await FluentActions.Awaiting(()=>rs.PublicAsync("spring","en",null,Ct)).Should().ThrowAsync<CatalogueRejection>();await FluentActions.Awaiting(()=>rs.PublicAsync("spring","en",release.Id,Ct)).Should().ThrowAsync<CatalogueRejection>();
        (await rs.ReleasesAsync(d.Id,Ct)).Single().WithdrawnAtUtc.Should().NotBeNull();(await ds.GetAsync(d.Id,Ct)).Revision.Should().Be(1);
        (await FluentActions.Awaiting(()=>rs.PublishAsync(d.Id,release.Id,1,Ct)).Should().ThrowAsync<CatalogueRejection>()).Which.StatusCode.Should().Be(409);
        var next=await rs.PrepareAsync(d.Id,1,await Fingerprint(rs,d.Id),new Dictionary<string,byte[]>(),Ct);await rs.PublishAsync(d.Id,next.Id,1,Ct);(await rs.ReleasesAsync(d.Id,Ct)).Count.Should().Be(2);
        Json(await rs.PublicAsync("spring","en",null,Ct)).GetProperty("id").GetGuid().Should().Be(next.Id);
    }
    [Fact] public async Task PdfParser_ShouldCheckCountAndGeometryAndRejectMagicOnlyFiles_WhenParsing()
    {
        await using var h=await CatalogueHarness.CreateAsync();var validator=h.Get<IDesignArtifactValidator>();
        validator.ValidatePdf(Pdf(2),[new(210,297),new(210,297)]);
        FluentActions.Invoking(()=>validator.ValidatePdf(Pdf(1),[new(210,297),new(210,297)])).Should().Throw<AppException>();
        FluentActions.Invoking(()=>validator.ValidatePdf(Pdf(1,100,100),[new(210,297)])).Should().Throw<AppException>();
        FluentActions.Invoking(()=>validator.ValidatePdf(Encoding.ASCII.GetBytes("%PDF-1.7\nnot a real PDF\n%%EOF"),[new(210,297)])).Should().Throw<AppException>();
    }
    [Fact] public async Task PublishedImages_ShouldRejectUnownedAndCorruptedMedia_WhenPublishing()
    {
        await using var h=await CatalogueHarness.CreateAsync();var unowned=await h.Get<IMediaStore>().StoreAsync(CatalogueHarness.Png(200),Ct);
        var n=JsonNode.Parse(Snapshot(web:false,pdf:true).GetRawText())!;n["pages"]![0]!["elements"]=JsonSerializer.SerializeToNode(new[]{new {id="photo",label="Photo",type="image",x=0,y=0,width=100,height=100,src=$"/api/catalogue/media/{unowned.Sha256}"}},CatalogueJson.Options);
        await FluentActions.Awaiting(()=>h.Designs.SaveAsync(null,new("Unowned",Json(n)),Ct)).Should().ThrowAsync<CatalogueRejection>();
        var row=await Seed(h);var ds=h.Designs;var rs=h.Releases;var d=await ds.SaveAsync(null,new("Spring",Snapshot(ids:[row.Id])),Ct);
        var release=await rs.PrepareAsync(d.Id,1,await Fingerprint(rs,d.Id),new Dictionary<string,byte[]>(),Ct);
        var doc=CatalogueJson.Read<ProductDocument>(row.Json);var blob=(await h.Get<ICatalogueRepository>().FindMediaAsync(doc.Media[0].Sha256,Ct))!;
        await File.WriteAllBytesAsync(Path.Combine(h.Root,"data","blobs",blob.BlobPath),Encoding.ASCII.GetBytes("corrupted"));
        await FluentActions.Awaiting(()=>rs.PreparationAsync(d.Id,Ct)).Should().ThrowAsync<CatalogueRejection>();await FluentActions.Awaiting(()=>rs.PublishAsync(d.Id,release.Id,1,Ct)).Should().ThrowAsync<CatalogueRejection>();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicMedia_ShouldPreservePinnedReleasesAndRejectPreparedAndRevokedMedia_WhenRequested(bool archive)
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
        (await mediaAccess.IsPublicAsync(original.Sha256, Ct)).Should().BeTrue();
        (await mediaAccess.IsPublicAsync(secondBlob.Sha256, Ct)).Should().BeTrue();
        (await mediaAccess.IsPublicAsync(preparedBlob.Sha256, Ct)).Should().BeFalse();
        if (archive) await designs.ArchiveAsync(design.Id, 1, Ct);
        else await releases.WithdrawAsync(design.Id, 1, Ct);
        (await mediaAccess.IsPublicAsync(original.Sha256, Ct)).Should().BeFalse();
        (await mediaAccess.IsPublicAsync(secondBlob.Sha256, Ct)).Should().BeFalse();
    }
    [Fact] public async Task PreparedWeb_ShouldPreserveCopyMediaGroupAndAllPriceFlags_WhenPrepared()
    {
        await using var h=await CatalogueHarness.CreateAsync();await Seed(h);var n=JsonNode.Parse(Snapshot(ids:["nimcha-v01"]).GetRawText())!;
        n["catalogueSettings"]=JsonSerializer.SerializeToNode(new {mediaGroup="model",priceDisplay=new {show=true,showCurrency=false,showQuantity=true,showRetail=false,showWholesale=true},copyByLocale=new Dictionary<string,object>{{"en",CatalogueDesignValidator.CopyFields.ToDictionary(key=>key,key=>key=="header"?"Spring atelier":key)}}},CatalogueJson.Options);
        var d=await h.Designs.SaveAsync(null,new("Spring",Json(n)),Ct);var rs=h.Releases;var r=await rs.PrepareAsync(d.Id,1,await Fingerprint(rs,d.Id),new Dictionary<string,byte[]>(),Ct);
        r.Web!.Value.GetProperty("copy").GetProperty("header").GetString().Should().Be("Spring atelier");r.Web.Value.GetProperty("priceDisplay").GetProperty("showRetail").GetBoolean().Should().BeFalse();r.Web.Value.GetProperty("productMedia").GetProperty("nimcha-v01").EnumerateArray().Should().ContainSingle();
    }
    [Fact] public async Task ReleaseArtifacts_ShouldLandInFilesOnceAndRedirectPublicRoute_WhenPublishedOnFiles()
    {
        var files=new TestFiles();await using var h=await CatalogueHarness.CreateAsync(files:files);await Seed(h);
        var n=JsonNode.Parse(Snapshot(web:true,pdf:true,ids:["nimcha-v01"]).GetRawText())!;
        n["pages"]=JsonSerializer.SerializeToNode(new[]{Page("cover",[]),Page("good",["nimcha-v01"])},CatalogueJson.Options);
        var ds=h.Designs;var rs=h.Releases;var designs=h.Get<IDesignRepository>();var d=await ds.SaveAsync(null,new("PDF",Json(n)),Ct);var fingerprint=await Fingerprint(rs,d.Id);
        var upload=new Dictionary<string,byte[]>{["catalogue.pdf"]=Pdf(2),["page-cover.pdf"]=Pdf(1),["page-good.pdf"]=Pdf(1),["page-cover.png"]=CatalogueHarness.Png(83,420,594),["page-good.png"]=CatalogueHarness.Png(84,420,594)};
        var imagesBefore=files.Uploads;var prepared=await rs.PrepareAsync(d.Id,1,fingerprint,upload,Ct);var uploaded=files.Uploads-imagesBefore;
        await rs.PrepareAsync(d.Id,1,fingerprint,upload,Ct);files.Uploads.Should().Be(imagesBefore+uploaded);
        uploaded.Should().Be(prepared.Assets.Values.Distinct().Count());
        await rs.PublishAsync(d.Id,prepared.Id,1,Ct);
        var pdfSha=prepared.Assets["catalogue.pdf"];var record=await designs.ReleaseFileAsync(pdfSha,Ct);
        record.Should().NotBeNull();(await designs.AssetAsync(pdfSha,Ct)).Should().BeNull();(await designs.AssetsWithoutFileAsync(Ct)).Should().BeEmpty();
        var model=files.Model(record!.FileId);model.Name.Should().Be("ocharo-spring-en.pdf");model.AppId.Should().Be("catalogue");model.ContentType.Should().Be("application/pdf");
        files.ReferencesOf(model.Id).Should().Contain(new FileReference("catalogue.release",pdfSha));
        var served=await rs.PublicAssetAsync("spring","en",prepared.Id,"catalogue.pdf",Ct);
        served.Bytes.Should().BeNull();served.Location.Should().Be(TestFiles.Link(model.Id,null,FileDisposition.Attachment));served.FileName.Should().Be("ocharo-spring-en.pdf");
        var controller=new PublishedDesignsController(h.Get<ISender>()){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        var redirect=(await controller.GetAsset("spring","page-good.png","en",prepared.Id,Ct)).Should().BeOfType<RedirectResult>().Which;
        var png=files.Model((await designs.ReleaseFileAsync(prepared.Assets["page-good.png"],Ct))!.FileId);
        redirect.Url.Should().Be(TestFiles.Link(png.Id,null,FileDisposition.Attachment).AbsoluteUri);png.Name.Should().Be("ocharo-spring-2.png");
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        await rs.WithdrawAsync(d.Id,1,Ct);await FluentActions.Awaiting(()=>rs.PublicAssetAsync("spring","en",prepared.Id,"catalogue.pdf",Ct)).Should().ThrowAsync<CatalogueRejection>();
    }
}
