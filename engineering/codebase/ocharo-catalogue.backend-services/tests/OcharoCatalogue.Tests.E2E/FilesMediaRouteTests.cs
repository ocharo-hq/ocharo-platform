using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using WoW.Two.Sdk.Beta.Be.Files.Store;

namespace OcharoCatalogue.Tests.E2E;

/// <summary>Catalogue on WoW2 Files (<c>Files:BaseUrl</c> set): the media route redirects a member to a short-lived Files
/// link and keeps deciding access each time. Files itself is <see cref="TestFiles"/>, an in-memory broker set.</summary>
public sealed class FilesMediaRouteTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-files-tests", Guid.NewGuid().ToString("N"));
    private readonly List<IAsyncDisposable> hosts = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var host in hosts)
            await host.DisposeAsync();
        try { Directory.Delete(root, true); } catch (IOException) { }
    }

    [Fact]
    public async Task MediaRoute_ShouldRedirectMemberToLinkAndKeepDeciding_WhenOnFiles()
    {
        var files = new TestFiles();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseCatalogueSignIn();
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OcharoCatalogue"] = $"Data Source={Path.Combine(root, "web.db")};Pooling=False",
                ["Catalogue:DataRoot"] = Path.Combine(root, "web"),
                ["Catalogue:SourceRoot"] = null,
                ["Files:BaseUrl"] = TestFiles.Url,
                ["DataProtection:KeysPath"] = Path.Combine(root, "web", "keys"),
            }));
            builder.ConfigureTestServices(services => files.Register(services));
        });
        hosts.Add(factory);
        var stored = await factory.Services.CreateScope().ServiceProvider.GetRequiredService<IMediaStore>().StoreAsync(CatalogueHarness.Png(90), default);
        using var member = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        using var outsider = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Outsider);

        var response = await member.GetAsync($"/api/catalogue/media/{stored.Sha256}/preview");
        var hidden = await outsider.GetAsync($"/api/catalogue/media/{stored.Sha256}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be(TestFiles.Link(FileId.TryParse(stored.FileId, out var id) ? id : default, 900));
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
