using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;
namespace OcharoCatalogue.Tests;

/// <summary>Local HTTPS host contract; each test creates its own database and image folder.</summary>
public sealed class CatalogueDesignApiTests
{
    [Fact] public async Task Native_routes_save_prepare_publish_and_archive_with_same_product_data()
    {
        var root=Path.Combine(Path.GetTempPath(),"catalogue-design-api",Guid.NewGuid().ToString("N"));
        await using var host=new WebApplicationFactory<Program>().WithWebHostBuilder(builder=>{
            builder.UseEnvironment("Testing");builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{
                ["ConnectionStrings:OcharoCatalogue"]=$"Data Source={Path.Combine(root,"api.db")};Pooling=False",["Catalogue:DataRoot"]=Path.Combine(root,"data"),["Catalogue:SeedOnEmpty"]="false"
            }));
        });
        using var client=host.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});
        using(var scope=host.Services.CreateScope())
        {
            var media=await scope.ServiceProvider.GetRequiredService<IMediaStore>().StoreAsync(CatalogueHarness.Png(121),default);
            var copy=new ProductLocale("approved","Vest","Silk",null,null);var product=new ProductDocument{Id="nimcha-v01",VariantCode="v01",GarmentType="nimcha",Status="active",Locales=new(copy,copy,copy),Media=[new("photo","model","photo.png","image/png",media.ByteLength,media.Sha256,media.Width,media.Height,true,null,null)]};
            await scope.ServiceProvider.GetRequiredService<ICatalogueRepository>().InsertProductAsync(ProductService.ToRecord(product,1,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow),"Seed",default);
        }
        var source=await client.GetFromJsonAsync<JsonElement>("/api/catalogue/design-source");Assert.Single(source.GetProperty("products").EnumerateArray());
        var snapshot=new {schemaVersion=1,kind="catalogue",templateId="blank",locale="en",pages=Array.Empty<object>(),catalogue=new {slug="spring",description="Spring selection",isActive=true,outputs=new {web=true,pdf=false},web=new {theme="grid",pages=new[]{new {id="one",title="Collection",layout="grid",isActive=true,productIds=new[]{"nimcha-v01"}}}}}};
        var created=await client.PostAsJsonAsync("/api/catalogue/designs",new {title="Spring",snapshot});Assert.Equal(HttpStatusCode.OK,created.StatusCode);var design=await created.Content.ReadFromJsonAsync<JsonElement>();var id=design.GetProperty("id").GetGuid();
        var list=await client.GetFromJsonAsync<JsonElement>("/api/catalogue/designs");Assert.Equal("spring",list[0].GetProperty("catalogue").GetProperty("slug").GetString());Assert.False(list[0].TryGetProperty("snapshot",out _));
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/public/catalogue/editions/spring?locale=en")).StatusCode);
        var preparation=await client.GetFromJsonAsync<JsonElement>($"/api/catalogue/designs/{id}/preparation");
        var prepared=await client.PostAsJsonAsync($"/api/catalogue/designs/{id}/prepare",new {expectedRevision=1,fingerprint=preparation.GetProperty("fingerprint").GetString()});Assert.Equal(HttpStatusCode.OK,prepared.StatusCode);var release=await prepared.Content.ReadFromJsonAsync<JsonElement>();var releaseId=release.GetProperty("id").GetGuid();
        var published=await client.PostAsJsonAsync($"/api/catalogue/designs/{id}/releases/{releaseId}/publish",new{expectedRevision=1});Assert.Equal(HttpStatusCode.OK,published.StatusCode);
        var publicView=await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue/editions/spring?locale=en");Assert.Equal("Vest",publicView.GetProperty("products")[0].GetProperty("name").GetString());Assert.Equal(JsonValueKind.Null,publicView.GetProperty("pdf").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/public/catalogue/editions/spring?locale=fr")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/public/catalogue/editions/spring?locale=not-a-language")).StatusCode);
        using var evil=new HttpRequestMessage(HttpMethod.Post,"/api/catalogue/designs"){Content=JsonContent.Create(new {title="Other",snapshot})};evil.Headers.Add("Origin","https://evil.example");Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(evil)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.DeleteAsync($"/api/catalogue/designs/{id}?expectedRevision=9")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.DeleteAsync($"/api/catalogue/designs/{id}?expectedRevision=1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync($"/api/public/catalogue/editions/spring?locale=en&release={releaseId}")).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/catalogue/products")).EnumerateArray());
    }
}
