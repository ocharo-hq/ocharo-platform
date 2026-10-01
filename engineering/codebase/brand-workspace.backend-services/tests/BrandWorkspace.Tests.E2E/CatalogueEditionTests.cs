using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Persistence.Commerce;
using OcharoBrand.Domain.Brand;
using SkiaSharp;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Real authenticated multipart, tenant, optimistic revision and immutable release artifact coverage.</summary>
public sealed class CatalogueEditionTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    private static readonly byte[] Pdf=Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <</Type /Catalog>> endobj\n%%EOF\n");
    private sealed record Setup(OrganizationAccess Owner,HttpClient Client,Guid Brand,Guid Design,string Path,string Slug);

    [Fact]
    public async Task Workflow_BindsSavedRevision_PreservesLegacyEdits_AndServesOnlyApprovedReleaseAssets()
    {
        var x=await Create();using var client=x.Client;
        using var attached=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,1));
        attached.StatusCode.Should().Be(HttpStatusCode.OK,await attached.Content.ReadAsStringAsync());
        var draft=await Data(attached);var edition=draft.GetProperty("snapshot").GetProperty("catalogueEditions")[0];
        edition.GetProperty("title").GetString().Should().Be("Saved source title");
        edition.GetProperty("sourceDesignId").GetGuid().Should().Be(x.Design);
        edition.GetProperty("sourceRevision").GetInt32().Should().Be(1);
        edition.GetProperty("approvedForPublication").GetBoolean().Should().BeFalse();
        edition.GetProperty("pages")[0].GetProperty("widthMm").GetDouble().Should().Be(210);
        edition.GetProperty("pdfSha256").GetString().Should().Be(Convert.ToHexStringLower(SHA256.HashData(Pdf)));
        using var duplicate=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,2));
        (await Data(duplicate)).GetProperty("revision").GetInt32().Should().Be(2,"same source and exact bytes are idempotent");
        // An older client drops the new property. The server must preserve its own artifacts.
        var legacy=JsonNode.Parse(draft.GetProperty("snapshot").GetRawText())!;legacy.AsObject().Remove("catalogueEditions");
        using var saved=await client.PutAsJsonAsync(x.Path,new{expectedRevision=2,snapshot=legacy});
        saved.StatusCode.Should().Be(HttpStatusCode.OK,await saved.Content.ReadAsStringAsync());
        (await Data(saved)).GetProperty("snapshot").GetProperty("catalogueEditions")[0].GetProperty("id").GetGuid().Should().Be(edition.GetProperty("id").GetGuid());
        // Generic owner edits cannot forge approval or an artifact hash.
        var forged=JsonNode.Parse((await Data(saved)).GetProperty("snapshot").GetRawText())!;
        forged["catalogueEditions"]![0]!["approvedForPublication"]=true;forged["catalogueEditions"]![0]!["pdfSha256"]=new string('f',64);
        using var ignored=await client.PutAsJsonAsync(x.Path,new{expectedRevision=3,snapshot=forged});
        ignored.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Data(ignored)).GetProperty("snapshot").GetProperty("catalogueEditions")[0].GetProperty("approvedForPublication").GetBoolean().Should().BeFalse();
        using var generic=await client.PostAsJsonAsync(x.Path+"/releases",new{expectedRevision=4,note="Brand only"});
        generic.StatusCode.Should().Be(HttpStatusCode.OK,await generic.Content.ReadAsStringAsync());
        var unapprovedRelease=(await Data(generic)).GetProperty("id").GetGuid();
        using var anon=fixture.Factory.CreateClient();
        var route=$"/api/public/v1/brands/{x.Slug}";
        var before=await anon.GetFromJsonAsync<JsonElement>(route+"/catalogue?locale=en");
        before.GetProperty("edition").ValueKind.Should().Be(JsonValueKind.Null);
        (await anon.GetAsync($"{route}/catalogue-editions/{edition.GetProperty("id").GetGuid()}/catalogue.pdf?release={unapprovedRelease}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var published=await client.PostAsJsonAsync(x.Path+"/catalogue-editions/en/publish",new{expectedRevision=4,note="Reviewed exact rendered files"});
        published.StatusCode.Should().Be(HttpStatusCode.OK,await published.Content.ReadAsStringAsync());
        var release=await Data(published);release.GetProperty("sourceRevision").GetInt32().Should().Be(5);
        var view=(await anon.GetFromJsonAsync<JsonElement>(route+"/catalogue?locale=en")).GetProperty("edition");
        view.GetProperty("pageCount").GetInt32().Should().Be(1);
        foreach(var url in new[]{view.GetProperty("pdfUrl").GetString()!,view.GetProperty("pages")[0].GetProperty("pdfUrl").GetString()!,view.GetProperty("pages")[0].GetProperty("pngUrl").GetString()!})
        {
            using var download=await anon.GetAsync(url+"&download=1");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            download.Headers.CacheControl!.NoStore.Should().BeTrue();
            download.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
            download.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
            (await download.Content.ReadAsByteArrayAsync()).Should().Equal(url.Contains(".png?")?Png():Pdf);
            (await anon.GetAsync(url.Split('?')[0])).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        var current=(await client.GetFromJsonAsync<JsonElement>(x.Path)).GetProperty("data");
        using var replaced=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,current.GetProperty("revision").GetInt32(),pdf:Pdf.Concat(Encoding.ASCII.GetBytes("\n")).ToArray()));
        replaced.StatusCode.Should().Be(HttpStatusCode.OK);
        // Replacement creates a new unapproved draft; the existing release remains immutable and reachable.
        (await anon.GetByteArrayAsync(view.GetProperty("pdfUrl").GetString()!)).Should().Equal(Pdf);
        using var withdrawn=await client.PostAsJsonAsync(x.Path+$"/releases/{release.GetProperty("id").GetGuid()}/withdraw",new{reason="Withdraw edition"});
        withdrawn.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await anon.GetAsync(view.GetProperty("pdfUrl").GetString()!)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SourceReferences_RejectForeignBrandAndUnknownProducts(bool foreignBrand)
    {
        object sourcePage=foreignBrand
            ?new{id="cover",title="Foreign",width=210,height=297,productIds=Array.Empty<string>(),catalogueSource=new{brandId=Guid.NewGuid()}}
            :new{id="cover",title="Unknown product",width=210,height=297,productIds=new[]{"not-a-brand-product"}};
        var x=await Create(sourcePage);using var client=x.Client;
        using var response=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,1));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,await response.Content.ReadAsStringAsync());
        (await client.GetFromJsonAsync<JsonElement>(x.Path)).GetProperty("data").GetProperty("revision").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task EditorialCoverWithoutProductIds_CanBeAttached()
    {
        var x=await Create(new{id="cover",title="Editorial cover",width=210,height=297});using var client=x.Client;
        using var response=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,1));
        response.StatusCode.Should().Be(HttpStatusCode.OK,await response.Content.ReadAsStringAsync());
        (await Data(response)).GetProperty("snapshot").GetProperty("catalogueEditions")[0].GetProperty("pages")[0].GetProperty("productIds").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task NullSnapshot_ReturnsValidationWithoutMutatingDraft()
    {
        var x=await Create();using var client=x.Client;
        using var response=await client.PutAsJsonAsync(x.Path,new{expectedRevision=1,snapshot=(object?)null});
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetFromJsonAsync<JsonElement>(x.Path)).GetProperty("data").GetProperty("revision").GetInt32().Should().Be(1);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("pdf")]
    [InlineData("png")]
    [InlineData("dimensions")]
    public async Task InvalidBundles_DoNotChangeDraftOrStoreArtifacts(string fault)
    {
        var x=await Create();using var client=x.Client;
        using var response=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,1,fault));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,await response.Content.ReadAsStringAsync());
        var current=(await client.GetFromJsonAsync<JsonElement>(x.Path)).GetProperty("data");
        current.GetProperty("revision").GetInt32().Should().Be(1);
        current.GetProperty("snapshot").GetProperty("catalogueEditions").GetArrayLength().Should().Be(0);
        await using var cmd=fixture.DataSource.CreateCommand("SELECT COUNT(*) FROM commerce.media WHERE brand_id=$1");cmd.Parameters.AddWithValue(x.Brand);
        Convert.ToInt32(await cmd.ExecuteScalarAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Access_RejectsForeignDesignsViewerAttachmentsEditorPublicationAndMissingCsrf()
    {
        var x=await Create();using var client=x.Client;
        var y=await Create();using var other=y.Client;
        using var foreign=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(y.Design,1));
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await fixture.SetRoleAsync(x.Owner,"viewer");
        using var viewer=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,1));viewer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await fixture.SetRoleAsync(x.Owner,"editor");
        using var attach=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,1));attach.StatusCode.Should().Be(HttpStatusCode.OK);
        using var editor=await client.PostAsJsonAsync(x.Path+"/catalogue-editions/en/publish",new{expectedRevision=2,note="Cannot approve"});editor.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var csrf=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,2));csrf.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StalePublishAndMissingBlob_RollBackApprovalAndRevision()
    {
        var x=await Create();using var client=x.Client;
        using var attach=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,1));attach.StatusCode.Should().Be(HttpStatusCode.OK);
        using var staleAttach=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,1));staleAttach.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var stale=await client.PostAsJsonAsync(x.Path+"/catalogue-editions/en/publish",new{expectedRevision=1,note="Stale"});stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using(var cmd=fixture.DataSource.CreateCommand("DELETE FROM commerce.media WHERE brand_id=$1")){cmd.Parameters.AddWithValue(x.Brand);await cmd.ExecuteNonQueryAsync();}
        using var missing=await client.PostAsJsonAsync(x.Path+"/catalogue-editions/en/publish",new{expectedRevision=2,note="Missing bytes"});missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var current=(await client.GetFromJsonAsync<JsonElement>(x.Path)).GetProperty("data");
        current.GetProperty("revision").GetInt32().Should().Be(2);
        current.GetProperty("snapshot").GetProperty("catalogueEditions")[0].GetProperty("approvedForPublication").GetBoolean().Should().BeFalse();
        (await client.GetFromJsonAsync<JsonElement>(x.Path+"/releases")).GetProperty("data").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ConcurrentSaveAndPublish_HaveOneWinnerWithoutPartialApproval()
    {
        var x=await Create();using var client=x.Client;
        using var attached=await client.PostAsync(x.Path+"/catalogue-editions",Bundle(x.Design,1));
        var snapshot=(await Data(attached)).GetProperty("snapshot");
        var publish=client.PostAsJsonAsync(x.Path+"/catalogue-editions/en/publish",new{expectedRevision=2,note="Race"});
        var save=client.PutAsJsonAsync(x.Path,new{expectedRevision=2,snapshot});
        var responses=await Task.WhenAll(publish,save);
        responses.Select(r=>r.StatusCode).Should().BeEquivalentTo(new[]{HttpStatusCode.OK,HttpStatusCode.Conflict});
        var current=(await client.GetFromJsonAsync<JsonElement>(x.Path)).GetProperty("data");
        current.GetProperty("revision").GetInt32().Should().Be(3);
        current.GetProperty("snapshot").GetProperty("catalogueEditions")[0].GetProperty("approvedForPublication").GetBoolean().Should().Be(responses[0].IsSuccessStatusCode);
        foreach(var response in responses)response.Dispose();
    }

    private async Task<Setup> Create(object? sourcePage=null)
    {
        var owner=await fixture.CreateOwnerAsync();var client=await fixture.LoginAsync(owner);
        using var brandResponse=await client.PostAsJsonAsync($"/api/organizations/{owner.OrganizationId}/brands",new{name="Edition fixture",timeZone="Asia/Tashkent"});
        var brand=(await Data(brandResponse)).GetProperty("id").GetGuid();var path=$"/api/organizations/{owner.OrganizationId}/brands/{brand}/commerce";
        var slug="edition-"+Guid.NewGuid().ToString("N");
        var snapshot=new CommerceSnapshot{Slug=slug,Identity=new BrandIdentity{Name="Edition fixture"}};
        using var saved=await client.PutAsJsonAsync(path,new{expectedRevision=0,snapshot});saved.StatusCode.Should().Be(HttpStatusCode.OK,await saved.Content.ReadAsStringAsync());
        using var design=await client.PostAsJsonAsync($"/api/organizations/{owner.OrganizationId}/design-documents",new{kind="catalogue",title="Saved source title",snapshot=new{
            schemaVersion=1,kind="catalogue",locale="en",templateId="fixture-template",pages=new[]{sourcePage??new{id="cover",title="Saved cover",width=210,height=297,productIds=Array.Empty<string>(),elements=Array.Empty<object>()}}
        }});
        design.StatusCode.Should().Be(HttpStatusCode.OK,await design.Content.ReadAsStringAsync());
        return new(owner,client,brand,(await Data(design)).GetProperty("id").GetGuid(),path,slug);
    }
    private static MultipartFormDataContent Bundle(Guid design,int revision,string? fault=null,byte[]? pdf=null)
    {
        var body=new MultipartFormDataContent();body.Add(new StringContent(JsonSerializer.Serialize(new{expectedRevision=revision,sourceDesignId=design,sourceRevision=1})),"metadata");
        body.Add(new ByteArrayContent(fault=="pdf"?[1,2,3]:pdf??Pdf),"pdf","catalogue.pdf");
        body.Add(new ByteArrayContent(fault=="png"?[1,2,3]:Png(fault=="dimensions"?210:297)),"page-cover.png","cover.png");
        if(fault!="missing")body.Add(new ByteArrayContent(pdf??Pdf),"page-cover.pdf","cover.pdf");
        if(fault is "extra" or "duplicate")body.Add(new ByteArrayContent(Pdf),fault=="extra"?"page-unknown.pdf":"page-cover.pdf","other.pdf");
        return body;
    }
    private static byte[] Png(int height=297)
    {
        using var bitmap=new SKBitmap(210,height);bitmap.Erase(SKColors.Beige);
        using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,100);return data.ToArray();
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response)=>JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync()).GetProperty("data");
}
