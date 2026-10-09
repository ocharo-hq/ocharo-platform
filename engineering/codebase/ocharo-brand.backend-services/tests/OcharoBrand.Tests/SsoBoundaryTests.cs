using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocharo.Sso;

namespace OcharoBrand.Tests;

public sealed class SsoBoundaryTests
{
    [Fact]
    public async Task EnabledSso_RequiresLiveBoundMembership_AndEnforcesReaderPermissions()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-brand-sso", Guid.NewGuid().ToString("N"));
        var organization = Guid.NewGuid();
        var broker = new FakeDesk(organization);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_,config) => config.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:OcharoBrand"] = $"Data Source={Path.Combine(root,"test.db")};Pooling=False",
                ["Brand:DataRoot"] = Path.Combine(root,"data"),
                ["Brand:SeedOnEmpty"] = "false",
                ["Brand:SeedLogoRoot"] = Path.Combine(root,"missing-logos"),
                ["Identity:Sso:Enabled"] = "true",
                ["Identity:Sso:DeskOrigin"] = "https://desk.example",
                ["Identity:Sso:ClientOrigin"] = "https://brand.example",
                ["Identity:Sso:ClientId"] = "brand",
                ["Identity:Sso:ClientSecret"] = new string('s',48),
                ["Identity:Sso:OrganizationId"] = organization.ToString()
            }));
            builder.ConfigureServices(services => services.AddHttpClient<SsoBackchannel>().ConfigurePrimaryHttpMessageHandler(() => broker));
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://brand.example"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/brand/draft")).StatusCode);
        var feed = await client.GetAsync("/api/public/brand");
        Assert.NotEqual(HttpStatusCode.Unauthorized,feed.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden,feed.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer","synthetic-integration-ticket");
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/brand/draft")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PutAsJsonAsync("/api/brand/identity",new { })).StatusCode);
        broker.CanEdit = true;
        var deniedPublication = await client.PostAsJsonAsync("/api/brand/publish",new { });
        Assert.Equal(HttpStatusCode.Forbidden,deniedPublication.StatusCode);
        Assert.Equal("sso_approval_required",(await deniedPublication.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        foreach (var path in new[] { "/api/brand/publish/", "/api/brand/PUBLISH/" })
        {
            var response = await client.PostAsJsonAsync(path, new { });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("sso_approval_required", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        broker.Organization = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/brand/draft")).StatusCode);
        broker.Organization = organization;
        broker.Active = false;
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/brand/draft")).StatusCode);
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
            Assert.Equal("ocharo-brand",json.GetProperty("audience").GetString());
            return new(HttpStatusCode.OK) { Content=JsonContent.Create(new { data = new {
                active=Active, userId="operator", organizationId=Organization, role="reader", canEdit=CanEdit, canApprove=false,
                expiresAt=DateTimeOffset.UtcNow.AddMinutes(5) } }) };
        }
    }
}
