using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator;
namespace OcharoCatalogue.Tests.E2E;

/// <summary>HTTPS host contract signed in as an owner; each test creates its own database and image folder.</summary>
public sealed class CatalogueDesignApiTests
{
    [Fact] public async Task DesignRoutes_ShouldSavePreparePublishAndArchive_WhenProductDataIsTheSame()
    {
        var root=Path.Combine(Path.GetTempPath(),"catalogue-design-api",Guid.NewGuid().ToString("N"));
        await using var host=new WebApplicationFactory<Program>().WithWebHostBuilder(builder=>{
            builder.UseEnvironment("Testing");builder.UseCatalogueSignIn();builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{
                ["ConnectionStrings:OcharoCatalogue"]=$"Data Source={Path.Combine(root,"api.db")};Pooling=False",["Catalogue:DataRoot"]=Path.Combine(root,"data"),["Catalogue:SeedOnEmpty"]="false"
            }));
        });
        using var client=CatalogueSignIn.CreateClient(host,CatalogueSignIn.Owner);
        using(var scope=host.Services.CreateScope())
        {
            var media=await scope.ServiceProvider.GetRequiredService<IMediaStore>().StoreAsync(CatalogueHarness.Png(121),default);
            var copy=new ProductLocale("approved","Vest","Silk",null,null);var product=new ProductDocument{Id="nimcha-v01",VariantCode="v01",GarmentType="nimcha",Status="active",Locales=new(copy,copy,copy),Media=[new("photo","model","photo.png","image/png",media.ByteLength,media.Sha256,media.Width,media.Height,true,null,null)]};
            await scope.ServiceProvider.GetRequiredService<ICatalogueRepository>().InsertProductAsync(ProductProjections.ToRecord(product,1,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow),"Seed",default);
        }
        var source=(await client.GetFromJsonAsync<JsonElement>("/api/catalogue/design-source")).GetProperty("data");source.GetProperty("products").EnumerateArray().Should().ContainSingle();
        var snapshot=new {schemaVersion=1,kind="catalogue",templateId="blank",locale="en",pages=Array.Empty<object>(),catalogue=new {slug="spring",description="Spring selection",isActive=true,outputs=new {web=true,pdf=false},web=new {theme="grid",pages=new[]{new {id="one",title="Collection",layout="grid",isActive=true,productIds=new[]{"nimcha-v01"}}}}}};
        var created=await client.PostAsJsonAsync("/api/catalogue/designs",new {title="Spring",snapshot});created.StatusCode.Should().Be(HttpStatusCode.OK);var design=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");var id=design.GetProperty("id").GetGuid();
        var list=(await client.GetFromJsonAsync<JsonElement>("/api/catalogue/designs")).GetProperty("data");list[0].GetProperty("catalogue").GetProperty("slug").GetString().Should().Be("spring");list[0].TryGetProperty("snapshot",out _).Should().BeFalse();
        (await client.GetAsync("/api/public/catalogue/editions/spring?locale=en")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var preparation=(await client.GetFromJsonAsync<JsonElement>($"/api/catalogue/designs/{id}/preparation")).GetProperty("data");
        var prepared=await client.PostAsJsonAsync($"/api/catalogue/designs/{id}/prepare",new {expectedRevision=1,fingerprint=preparation.GetProperty("fingerprint").GetString()});prepared.StatusCode.Should().Be(HttpStatusCode.OK);var release=(await prepared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");var releaseId=release.GetProperty("id").GetGuid();
        var published=await client.PostAsJsonAsync($"/api/catalogue/designs/{id}/releases/{releaseId}/publish",new{expectedRevision=1});published.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicView=(await client.GetFromJsonAsync<JsonElement>("/api/public/catalogue/editions/spring?locale=en")).GetProperty("data");publicView.GetProperty("products")[0].GetProperty("name").GetString().Should().Be("Vest");publicView.TryGetProperty("pdf",out _).Should().BeFalse();
        (await client.GetAsync("/api/public/catalogue/editions/spring?locale=fr")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/public/catalogue/editions/spring?locale=not-a-language")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var evil=new HttpRequestMessage(HttpMethod.Post,"/api/catalogue/designs"){Content=JsonContent.Create(new {title="Other",snapshot})};evil.Headers.Add("Origin","https://evil.example");(await client.SendAsync(evil)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"/api/catalogue/designs/{id}?expectedRevision=9")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"/api/catalogue/designs/{id}?expectedRevision=1")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/public/catalogue/editions/spring?locale=en&release={releaseId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetFromJsonAsync<JsonElement>("/api/catalogue/products")).GetProperty("data").EnumerateArray().Should().ContainSingle();
    }
}
