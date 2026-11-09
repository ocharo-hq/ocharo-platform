using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Persistence;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;
using WoW.Two.Sdk.Beta.Be.Mediator;

namespace OcharoCatalogue.Tests.E2E;

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
    public async Task Anonymous_ShouldReadOnlyPublicRoutesAndNeverWrite_WhenNoSessionExists()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CatalogueSignIn.CreateClient(host.Factory, person: null);

        foreach (var path in new[] { "/api/public/catalogue", "/api/public/catalogue/editions", "/api/runtime-config", "/api/system/status",
                     "/health" })
            (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/catalogue/products")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/catalogue/designs")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsJsonAsync("/api/catalogue/settings", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync(Publish, new { expectedRevision = 1 })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // The SPA shell and the published reader's page load without a session; the SPA itself starts sign-in.
        foreach (var path in new[] { "/", "/products", "/c/spring" })
            (await client.GetAsync(path)).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SessionRoute_ShouldAnswerSignedOut_WhenNoSessionExists()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CatalogueSignIn.CreateClient(host.Factory, person: null);

        using var response = await client.GetAsync("/api/identity/session");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("data").GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task SignalRoute_ShouldAnswer401_WhenPostIsUnsignedWithoutSession()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?> { ["Identity:Client:SignalUrl"] = "https://app.test/api/identity/signal" });
        using var client = CatalogueSignIn.CreateClient(host.Factory, person: null);

        var response = await client.PostAsJsonAsync("/api/identity/signal", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(CatalogueSignIn.Outsider)]
    [InlineData(CatalogueSignIn.Unaffiliated)]
    public async Task PrivateRoutes_ShouldAnswer403_WhenPersonIsOutsideTheBoundOrganization(string person)
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CatalogueSignIn.CreateClient(host.Factory, person);

        (await client.GetAsync("/api/catalogue/products")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync(Publish, new { expectedRevision = 1 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/public/catalogue")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Viewer_ShouldReadButNotWrite_WhenSignedIn()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CatalogueSignIn.CreateClient(host.Factory, CatalogueSignIn.Viewer);

        (await client.GetAsync("/api/catalogue/products")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/catalogue/settings")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.DeleteAsync("/api/catalogue/collections/any")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editor_ShouldWriteButNeedApprovalToPublishOrWithdraw_WhenSignedIn()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var editor = CatalogueSignIn.CreateClient(host.Factory, CatalogueSignIn.Editor);
        using var owner = CatalogueSignIn.CreateClient(host.Factory, CatalogueSignIn.Owner);

        (await editor.PostAsJsonAsync("/api/catalogue/products", new { garmentType = "nimcha" })).StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (var path in new[] { Publish, Publish + "/", Publish.Replace("publish", "PUBLISH"), Withdraw, Withdraw + "/" })
        {
            (await editor.PostAsJsonAsync(path, new { expectedRevision = 1 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            // The owner passes the approval check; the missing design then answers on its own.
            var approved = (await owner.PostAsJsonAsync(path, new { expectedRevision = 1 })).StatusCode;
            approved.Should().NotBe(HttpStatusCode.Unauthorized);
            approved.Should().NotBe(HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task Host_ShouldRefuseToBoot_WhenNoOrganizationIsBound()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?> { ["Catalogue:OrganizationId"] = Guid.Empty.ToString() });

        FluentActions.Invoking(() => host.Factory.CreateClient()).Should().Throw<Exception>();
    }

    [Fact]
    public async Task DataProtection_ShouldKeepApplicationNameAndShareKeysAndSessionsInPostgres_WhenHostRestarts()
    {
        var database = await CatalogueTestDatabase.CreateAsync();
        var settings = new Dictionary<string, string?> { [CatalogueTestDatabase.ConnectionKey] = database };
        string protectedText;
        await using (var first = CatalogueHost.Create(settings))
        {
            var services = first.Factory.Services;
            var discriminator = services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator;
            var keys = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository;

            // A changed name would sign every person out.
            discriminator.Should().Be("ocharo-catalogue:Testing");
            keys.Should().BeOfType<PostgresXmlRepository>();
            services.GetRequiredService<IDistributedCache>().Should().BeOfType<PostgresDistributedCache>();
            protectedText = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("session").Protect("signed in");
            await services.GetRequiredService<IDistributedCache>().SetStringAsync("session-key", "kept");
        }

        // A replaced container reads the same ring and the same sessions.
        await using var second = CatalogueHost.Create(settings);
        second.Factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("session").Unprotect(protectedText)
            .Should().Be("signed in");
        (await second.Factory.Services.GetRequiredService<IDistributedCache>().GetStringAsync("session-key")).Should().Be("kept");
    }

    [Fact]
    public async Task Media_ShouldServePublicBytesToAnyoneAndPrivateBytesOnlyToMembers_WhenRequested()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var anonymous = CatalogueSignIn.CreateClient(host.Factory, person: null);
        using var outsider = CatalogueSignIn.CreateClient(host.Factory, CatalogueSignIn.Outsider);
        using var viewer = CatalogueSignIn.CreateClient(host.Factory, CatalogueSignIn.Viewer);
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
            await repository.InsertProductAsync(ProductProjections.ToRecord(product, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), "Fixture", default);
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.CacheControl?.NoCache.Should().BeTrue();
            response.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be("*");
            foreach (var hash in privateHashes)
                foreach (var client in new[] { anonymous, outsider })
                {
                    var denied = await client.GetAsync($"/api/catalogue/media/{hash}{suffix}");
                    denied.StatusCode.Should().Be(HttpStatusCode.NotFound);
                    denied.Headers.CacheControl?.NoStore.Should().BeTrue();
                }
        }
        var draft = await viewer.GetAsync($"/api/catalogue/media/{privateHashes[0]}");
        draft.StatusCode.Should().Be(HttpStatusCode.OK);
        draft.Headers.CacheControl?.Private.Should().BeTrue();
        draft.Headers.CacheControl?.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Responses_ShouldKeepPublicFilesEmbeddableAndRestSameOrigin_WhenRequested()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CatalogueSignIn.CreateClient(host.Factory, person: null);

        using var media = await client.GetAsync($"/api/catalogue/media/{new string('f', 64)}/preview");
        using var release = await client.GetAsync($"/api/public/catalogue/editions/spring/assets/catalogue.pdf?locale=en&release={Guid.NewGuid()}");
        using var feed = await client.GetAsync("/api/public/catalogue");
        using var shell = await client.GetAsync("/c/spring");

        media.Headers.GetValues("Cross-Origin-Resource-Policy").Single().Should().Be("cross-origin");
        release.Headers.GetValues("Cross-Origin-Resource-Policy").Single().Should().Be("cross-origin");
        feed.Headers.GetValues("Cross-Origin-Resource-Policy").Single().Should().Be("same-origin");
        shell.Headers.GetValues("Cross-Origin-Resource-Policy").Single().Should().Be("same-origin");
    }

    [Fact]
    public async Task Responses_ShouldKeepNosniffAndNoStoreOnPrivateApi_WhenRequested()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CatalogueSignIn.CreateClient(host.Factory, CatalogueSignIn.Viewer);

        using var products = await client.GetAsync("/api/catalogue/products");
        using var feed = await client.GetAsync("/api/public/catalogue");

        products.Headers.CacheControl!.NoStore.Should().BeTrue();
        products.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
        feed.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
        feed.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be("*");
    }
}
