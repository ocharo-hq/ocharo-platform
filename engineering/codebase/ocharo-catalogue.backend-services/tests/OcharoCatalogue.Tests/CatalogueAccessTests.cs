using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;
using static OcharoCatalogue.Tests.CatalogueSignIn;

namespace OcharoCatalogue.Tests;

/// <summary>
/// Sign-in through Ocharo's Identity: the private API needs a member of the bound organization, unsafe methods
/// <c>catalogue:edit</c> and publication <c>catalogue:approve</c>; the public routes stay anonymous, and no anonymous
/// caller writes.
/// </summary>
public sealed class CatalogueAccessTests
{
    private const string Publish = "/api/catalogue/designs/00000000-0000-0000-0000-000000000001/releases/00000000-0000-0000-0000-000000000002/publish";
    private const string Withdraw = "/api/catalogue/designs/00000000-0000-0000-0000-000000000001/withdraw";

    [Fact]
    public async Task Anonymous_ShouldReadOnlyThePublicRoutes_AndNeverWrite()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CreateClient(host.Factory, person: null);

        foreach (var path in new[] { "/api/public/catalogue", "/api/public/catalogue/editions", "/api/runtime-config", "/api/system/status",
                     "/api/system/version", "/health" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/catalogue/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/catalogue/designs")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/catalogue/settings", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Publish, new { expectedRevision = 1 })).StatusCode);
        // The SPA shell and the published reader's page load without a session; the SPA itself starts sign-in.
        foreach (var path in new[] { "/", "/products", "/c/spring" })
            Assert.NotEqual(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task SessionRoute_ShouldAnswerSignedOut_WithoutASession()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CreateClient(host.Factory, person: null);

        using var response = await client.GetAsync("/api/identity/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("data").GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task SignalRoute_ShouldAnswer401_ForAnUnsignedPostWithoutASession()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?> { ["Identity:Client:SignalUrl"] = "https://app.test/api/identity/signal" });
        using var client = CreateClient(host.Factory, person: null);

        var response = await client.PostAsJsonAsync("/api/identity/signal", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Outsider)]
    [InlineData(Unaffiliated)]
    public async Task PersonOutsideTheBoundOrganization_ShouldBeForbiddenEverywherePrivate(string person)
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CreateClient(host.Factory, person);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/catalogue/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Publish, new { expectedRevision = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/public/catalogue")).StatusCode);
    }

    [Fact]
    public async Task Viewer_ShouldRead_ButNotWrite()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CreateClient(host.Factory, Viewer);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/catalogue/products")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/catalogue/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/catalogue/collections/any")).StatusCode);
    }

    [Fact]
    public async Task Editor_ShouldWrite_ButNeedApprovalToPublishOrWithdraw()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var editor = CreateClient(host.Factory, Editor);
        using var owner = CreateClient(host.Factory, Owner);

        Assert.Equal(HttpStatusCode.OK, (await editor.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" })).StatusCode);
        foreach (var path in new[] { Publish, Publish + "/", Publish.Replace("publish", "PUBLISH"), Withdraw, Withdraw + "/" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync(path, new { expectedRevision = 1 })).StatusCode);
            // The owner passes the approval check; the missing design then answers on its own.
            var approved = (await owner.PostAsJsonAsync(path, new { expectedRevision = 1 })).StatusCode;
            Assert.NotEqual(HttpStatusCode.Unauthorized, approved);
            Assert.NotEqual(HttpStatusCode.Forbidden, approved);
        }
    }

    [Fact]
    public async Task Host_ShouldRefuseToBoot_WithoutABoundOrganization()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?> { ["Catalogue:OrganizationId"] = Guid.Empty.ToString() });

        Assert.ThrowsAny<Exception>(() => host.Factory.CreateClient());
    }

    [Fact]
    public async Task Media_ShouldServePublicBytesToAnyone_AndPrivateBytesOnlyToMembers()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var anonymous = CreateClient(host.Factory, person: null);
        using var outsider = CreateClient(host.Factory, Outsider);
        using var viewer = CreateClient(host.Factory, Viewer);
        using var scope = host.Factory.Services.CreateScope();
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
            var response = await anonymous.GetAsync($"/api/catalogue/media/{publicHash}{suffix}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoCache);
            Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
            foreach (var hash in privateHashes)
                foreach (var client in new[] { anonymous, outsider })
                {
                    var denied = await client.GetAsync($"/api/catalogue/media/{hash}{suffix}");
                    Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
                    Assert.True(denied.Headers.CacheControl?.NoStore);
                }
        }
        var draft = await viewer.GetAsync($"/api/catalogue/media/{privateHashes[0]}");
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        Assert.True(draft.Headers.CacheControl?.Private);
        Assert.True(draft.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Responses_ShouldKeepNosniff_AndNoStoreOnThePrivateApi()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CreateClient(host.Factory, Viewer);

        using var products = await client.GetAsync("/api/catalogue/products");
        using var feed = await client.GetAsync("/api/public/catalogue");

        Assert.True(products.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", products.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("nosniff", feed.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("*", feed.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
