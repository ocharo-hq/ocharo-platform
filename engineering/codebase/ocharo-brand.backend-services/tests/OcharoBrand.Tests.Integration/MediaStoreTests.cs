using Microsoft.Extensions.DependencyInjection;
using OcharoBrand.Application.Brand.Services;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoBrand.Tests.Integration;

/// <summary>The media store over a real SQLite store and an in-memory Files tenant (<see cref="TestFiles"/>).</summary>
public sealed class MediaStoreTests : IAsyncDisposable
{
    private readonly FilesWorkspace workspace = new();

    public async ValueTask DisposeAsync() => await workspace.DisposeAsync();

    [Fact]
    public async Task StoreAsync_ShouldRestoreOrReuploadLogo_WhenItsFileWasDeletedInFiles()
    {
        var files = new TestFiles();
        var factory = workspace.Host(files);
        using var anonymous = BrandTestIdentity.Anonymous(factory);
        await anonymous.GetAsync("/api/public/brand");
        var bytes = TestArtwork.Png(10);
        var first = files.BySha(TestArtwork.Sha(bytes));
        var media = factory.Services.CreateScope().ServiceProvider.GetRequiredService<IMediaStore>();

        await files.DeleteAsync(first.Id);
        var restored = (await media.StoreAsync(bytes, default)).ValueOrThrow();
        var live = files.ById(first.Id).DeletedAtUtc;
        await files.DeleteAsync(first.Id);
        files.RestoreFails = true;
        var replaced = (await media.StoreAsync(bytes, default)).ValueOrThrow();

        restored.FileId.Should().Be(first.Id.ToString());
        live.Should().BeNull();
        replaced.FileId.Should().NotBe(first.Id.ToString());
        (FileId.TryParse(replaced.FileId, out var id) && files.ById(id).DeletedAtUtc is null).Should().BeTrue();
    }
}
