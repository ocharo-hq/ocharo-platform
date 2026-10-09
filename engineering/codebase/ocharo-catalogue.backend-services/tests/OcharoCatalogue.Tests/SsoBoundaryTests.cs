using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocharo.Sso;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Tests;

public sealed class SsoBoundaryTests
{
    [Fact]
    public async Task EnabledSso_RequiresLiveBoundMembership_AndEnforcesReaderPermissions()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-sso", Guid.NewGuid().ToString("N"));
        var organization = Guid.NewGuid();
        var broker = new FakeDesk(organization);
        await using var factory = CreateFactory(root, organization, broker);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://catalogue.example"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/catalogue/products")).StatusCode);
        var feed = await client.GetAsync("/api/public/catalogue");
        Assert.NotEqual(HttpStatusCode.Unauthorized,feed.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden,feed.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer","synthetic-integration-ticket");
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/catalogue/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PutAsJsonAsync("/api/catalogue/products",new { })).StatusCode);
        broker.CanEdit = true;
        var deniedPublication = await client.PostAsJsonAsync("/api/catalogue/designs/00000000-0000-0000-0000-000000000001/releases/00000000-0000-0000-0000-000000000002/publish",new { });
        Assert.Equal(HttpStatusCode.Forbidden,deniedPublication.StatusCode);
        Assert.Equal("sso_approval_required",(await deniedPublication.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        foreach (var path in new[] { "/api/catalogue/designs/00000000-0000-0000-0000-000000000001/releases/00000000-0000-0000-0000-000000000002/publish/", "/api/catalogue/designs/00000000-0000-0000-0000-000000000001/releases/00000000-0000-0000-0000-000000000002/PUBLISH/", "/api/catalogue/designs/00000000-0000-0000-0000-000000000001/withdraw/" })
        {
            var response = await client.PostAsJsonAsync(path, new { });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("sso_approval_required", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        broker.Organization = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/catalogue/products")).StatusCode);
        broker.Organization = organization;
        broker.Active = false;
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/catalogue/products")).StatusCode);
    }
    [Fact]
    public async Task Media_RequiresPublicEligibility_OrLiveSession_AndNeverCachesPrivateBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-sso", Guid.NewGuid().ToString("N"));
        var organization = Guid.NewGuid();
        var broker = new FakeDesk(organization);
        await using var factory = CreateFactory(root, organization, broker);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://catalogue.example"), AllowAutoRedirect = false });
        using var scope = factory.Services.CreateScope();
        var media = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        var repository = scope.ServiceProvider.GetRequiredService<ICatalogueRepository>();
        async Task<string> Seed(int index, string status, bool approved, bool active = true, bool review = false)
        {
            var blob = await media.StoreAsync(CatalogueHarness.Png((byte)(31 + index)), default);
            var product = new ProductDocument
            {
                Id = $"nimcha-v{index}", GarmentType = "nimcha", VariantCode = $"v{index}", Status = status,
                Locales = new(new(approved ? LocaleStatus.Approved : LocaleStatus.Draft, "Nimcha", null, null, null), ProductLocale.Empty(), ProductLocale.Empty()),
                Media = [new("front", MediaRole.Cutout, "front.png", "image/png", blob.ByteLength, blob.Sha256, blob.Width, blob.Height, true, "Front", null, active)],
                Issues = review ? [new("review", "fabric", IssueStatus.Open, "Confirm fabric")] : []
            };
            await repository.InsertProductAsync(ProductService.ToRecord(product, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), "Fixture", default);
            return blob.Sha256;
        }
        var publicHash = await Seed(1, ProductStatus.Active, true);
        var privateHashes = new[]
        {
            await Seed(2, ProductStatus.Draft, true),
            await Seed(3, ProductStatus.Active, false),
            await Seed(4, ProductStatus.Active, true, active: false),
            await Seed(5, ProductStatus.Active, true, review: true),
            (await media.StoreAsync(CatalogueHarness.Png(90), default)).Sha256,
            new string('f', 64)
        };
        foreach (var suffix in new[] { "", "/preview" })
        {
            var response = await client.GetAsync($"/api/catalogue/media/{publicHash}{suffix}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoCache);
            foreach (var hash in privateHashes)
            {
                var denied = await client.GetAsync($"/api/catalogue/media/{hash}{suffix}");
                Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                Assert.True(denied.Headers.CacheControl?.NoStore);
            }
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-integration-ticket");
        var draft = await client.GetAsync($"/api/catalogue/media/{privateHashes[0]}");
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        Assert.True(draft.Headers.CacheControl?.Private);
        Assert.True(draft.Headers.CacheControl?.NoStore);
        broker.Active = false;
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/catalogue/media/{privateHashes[0]}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/catalogue/media/{publicHash}")).StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string root, Guid organization, FakeDesk broker)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_,config) => config.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:OcharoCatalogue"] = $"Data Source={Path.Combine(root,"test.db")};Pooling=False",
                ["Catalogue:DataRoot"] = Path.Combine(root,"data"),
                ["Catalogue:SeedOnEmpty"] = "false",
                ["Brand:SeedLogoRoot"] = Path.Combine(root,"missing-logos"),
                ["Identity:Sso:Enabled"] = "true",
                ["Identity:Sso:DeskOrigin"] = "https://desk.example",
                ["Identity:Sso:ClientOrigin"] = "https://catalogue.example",
                ["Identity:Sso:ClientId"] = "catalogue",
                ["Identity:Sso:ClientSecret"] = new string('s',48),
                ["Identity:Sso:OrganizationId"] = organization.ToString()
            }));
            builder.ConfigureServices(services => services.AddHttpClient<SsoBackchannel>().ConfigurePrimaryHttpMessageHandler(() => broker));
        });
    }

    private sealed class FakeDesk(Guid organization) : HttpMessageHandler
    {
        public bool Active = true;
        public bool CanEdit;
        public Guid Organization = organization;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://desk.example/api/integrations/introspect",request.RequestUri!.AbsoluteUri);
            var json = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken:cancellationToken);
            Assert.Equal("ocharo-catalogue",json.GetProperty("audience").GetString());
            return new(HttpStatusCode.OK) { Content=JsonContent.Create(new { data = new {
                active=Active, userId="operator", organizationId=Organization, role="reader", canEdit=CanEdit, canApprove=false,
                expiresAt=DateTimeOffset.UtcNow.AddMinutes(5) } }) };
        }
    }
}
