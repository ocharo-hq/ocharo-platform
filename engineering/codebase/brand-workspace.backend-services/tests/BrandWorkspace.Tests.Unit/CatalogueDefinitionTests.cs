using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.Commerce;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using OcharoBrand.Domain.Brand;
using OcharoCatalogue.Domain.Catalogue.Models;
using SkiaSharp;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>Saved catalogue output selection and public eligibility use the same canonical products.</summary>
public sealed class CatalogueDefinitionTests
{
    private static readonly byte[] Pdf=Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <</Type /Catalog>> endobj\n%%EOF\n");
    private static CommerceProduct Product(string id,bool approved)=>new(new ProductDocument {
        Id=id,GarmentType="vest",VariantCode="v01",Status="active",Locales=new(ProductLocale.Empty(),ProductLocale.Empty(),new(approved?"approved":"draft","Vest","Silk",null,null))
    });
    private static CommerceSnapshot Products()=>new(){Slug="brand",Identity=new BrandIdentity{Name="Brand"},Products=[Product("approved",true),Product("draft",false)]};
    private static JsonObject Snapshot(Guid brand)=>JsonSerializer.SerializeToNode(new {
        kind="catalogue",locale="en",templateId="catalogue",catalogueSettings=new {brandId=brand,mediaGroup="all",priceDisplay=new{show=true,showCurrency=true,showQuantity=true,showRetail=true,showWholesale=true},copyByLocale=new{}},
        catalogue=new {slug="summer",description="Season",isActive=true,outputs=new{web=true,pdf=true},web=new{theme="grid",pages=new[]{new{id="looks",title="Looks",layout="spread",isActive=true,productIds=new[]{"approved","draft"}}}}},
        pages=new object[]{new{id="cover",title="Cover",width=210,height=297,productIds=Array.Empty<string>(),elements=Array.Empty<object>()},
            new{id="mixed",title="Mixed",width=210,height=297,productIds=new[]{"approved"},elements=new[]{new{type="text",productId="draft",binding=new{kind="product",field="name"}}}},
            new{id="inactive",title="Hidden",width=210,height=297,isActive=false,productIds=new[]{"approved"},elements=Array.Empty<object>()}}
    })!.AsObject();
    private static DesignDocument Design(JsonObject snapshot)=>new(Guid.NewGuid(),Guid.NewGuid(),"catalogue","Summer",JsonSerializer.SerializeToElement(snapshot),1,false,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);
    private static byte[] Png()
    {
        using var bitmap=new SKBitmap(210,297);bitmap.Erase(SKColors.Beige);
        using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);return data.ToArray();
    }
    [Fact]
    public void OutputSelection_DropsWholeMixedPdfPage_ButRetainsConfirmedWebProducts()
    {
        var brand=Guid.NewGuid();var snapshot=Snapshot(brand);var files=new Dictionary<string,byte[]>{{"pdf",Pdf},{"page-cover.png",Png()},{"page-cover.pdf",Pdf}};
        var result=CatalogueEditionBuilder.Build(Design(snapshot),brand,files,DateTimeOffset.UtcNow,Products());
        result.Edition.Pages.Select(page=>page.Id).Should().Equal("cover");
        result.Edition.Web!.Pages[0].ProductIds.Should().Equal("approved");
        result.Edition.Slug.Should().Be("summer");
        files["page-mixed.png"]=Png();files["page-mixed.pdf"]=Pdf;
        Action includesHiddenPage=()=>CatalogueEditionBuilder.Build(Design(snapshot),brand,files,DateTimeOffset.UtcNow,Products());
        includesHiddenPage.Should().Throw<PreparationException>();
    }
    [Fact]
    public void EmptyOutputs_CanSaveDraft_ButCannotPrepareAnEmptyEdition()
    {
        var brand=Guid.NewGuid();var snapshot=Snapshot(brand);snapshot["pages"]=new JsonArray();snapshot["catalogue"]!["outputs"]!["pdf"]=false;snapshot["catalogue"]!["web"]!["pages"]=new JsonArray();
        var design=Design(snapshot);var validation=new DesignDocumentValidationService();
        Action save=()=>validation.ValidateInput(new DesignDocumentInput(design.Title,design.Snapshot));save.Should().NotThrow();
        Action prepare=()=>CatalogueEditionBuilder.Build(design,brand,new Dictionary<string,byte[]>(),DateTimeOffset.UtcNow,Products());prepare.Should().Throw<PreparationException>();
    }
    [Fact]
    public void WebOnly_PreparesNoArtifacts_AndRejectsUnknownProductReferences()
    {
        var brand=Guid.NewGuid();var snapshot=Snapshot(brand);snapshot["catalogue"]!["outputs"]!["pdf"]=false;snapshot["pages"]=new JsonArray();
        var result=CatalogueEditionBuilder.Build(Design(snapshot),brand,new Dictionary<string,byte[]>(),DateTimeOffset.UtcNow,Products());
        result.Edition.PdfSha256.Should().BeNull();result.Blobs.Should().BeEmpty();
        snapshot["catalogue"]!["web"]!["pages"]![0]!["productIds"]=new JsonArray("another-brand-product");
        Action foreign=()=>CatalogueEditionBuilder.Build(Design(snapshot),brand,new Dictionary<string,byte[]>(),DateTimeOffset.UtcNow,Products());foreign.Should().Throw<PreparationException>();
    }
    [Fact]
    public void WebPresentation_PreservesCopyPricesAndSelectedApprovedImageGroup()
    {
        var brand=Guid.NewGuid();var snapshot=Snapshot(brand);snapshot["catalogue"]!["outputs"]!["pdf"]=false;snapshot["pages"]=new JsonArray();
        snapshot["catalogueSettings"]!["mediaGroup"]="model";
        snapshot["catalogueSettings"]!["priceDisplay"]!["showRetail"]=false;
        snapshot["catalogueSettings"]!["copyByLocale"]!["en"]=JsonSerializer.SerializeToNode(CatalogueWebSettings.CopyKeys.ToDictionary(key=>key,key=>key+" edited"));
        var products=Products();var product=products.Products[0];var approvedHash=new string('a',64);var privateHash=new string('b',64);var otherHash=new string('c',64);
        product=product with {Document=product.Document with {Media=[new("model","model","model.png","image/png",1,approvedHash,1,1,false,null,null),new("private","model","private.png","image/png",1,privateHash,1,1,false,null,null),new("cutout","cutout","cutout.png","image/png",1,otherHash,1,1,false,null,null)]}};
        products=products with {Products=[product,products.Products[1]],MediaApprovals=new Dictionary<string,MediaApproval>{{approvedHash,new("photo",true)},{otherHash,new("photo",true)}}};
        var built=CatalogueEditionBuilder.Build(Design(snapshot),brand,new Dictionary<string,byte[]>(),DateTimeOffset.UtcNow,products).Edition;
        built.Web!.Copy["footer"].Should().Be("footer edited");built.Web.PriceDisplay.ShowRetail.Should().BeFalse();built.Web.MediaGroup.Should().Be("model");
        built.Web.ProductMedia["approved"].Should().Equal(approvedHash);
        var release=new CommerceRelease(Guid.NewGuid(),brand,1,1,DateTimeOffset.UtcNow,"Reviewed",products with {CatalogueEditions=[built with {ApprovedForPublication=true}]});
        var publicView=JsonSerializer.SerializeToElement(PublicProjection.Catalogue(release,"en"),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        publicView.GetProperty("catalogues")[0].GetProperty("web").GetProperty("copy").GetProperty("footer").GetString().Should().Be("footer edited");
        publicView.GetProperty("catalogues")[0].GetProperty("web").GetProperty("productMedia").GetProperty("approved").EnumerateArray().Select(hash=>hash.GetString()).Should().Equal(approvedHash);
    }
    [Theory]
    [InlineData("flag")]
    [InlineData("slug")]
    [InlineData("layout")]
    [InlineData("duplicate")]
    [InlineData("both-disabled")]
    [InlineData("page-scalar")]
    public void NamedMetadata_RejectsMalformedState(string fault)
    {
        var snapshot=Snapshot(Guid.NewGuid());
        switch(fault){
            case "flag":snapshot["pages"]![0]!["isActive"]="yes";break;
            case "slug":snapshot["catalogue"]!["slug"]="pages";break;
            case "layout":snapshot["catalogue"]!["web"]!["pages"]![0]!["layout"]="unknown";break;
            case "duplicate":snapshot["catalogue"]!["web"]!["pages"]![0]!["productIds"]=new JsonArray("approved","approved");break;
            case "both-disabled":snapshot["catalogue"]!["outputs"]!["web"]=false;snapshot["catalogue"]!["outputs"]!["pdf"]=false;break;
            case "page-scalar":snapshot["catalogue"]!["web"]!["pages"]![0]=1;break;
        }
        Action save=()=>new DesignDocumentValidationService().ValidateInput(new DesignDocumentInput("Summer",JsonSerializer.SerializeToElement(snapshot)));
        save.Should().Throw<PreparationException>();
    }
}
