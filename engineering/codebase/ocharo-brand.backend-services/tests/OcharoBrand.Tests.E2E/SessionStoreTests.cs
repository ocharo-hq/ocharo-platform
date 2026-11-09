using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OcharoBrand.Persistence;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;

namespace OcharoBrand.Tests.E2E;

/// <summary>Sign-in sessions and the key ring that encrypts their cookies live in the product database, so a restarted or
/// replaced Brand reads what the one before it wrote and nobody is signed out.</summary>
public sealed class SessionStoreTests : IAsyncDisposable
{
    private readonly FilesWorkspace workspace = new();

    public async ValueTask DisposeAsync() => await workspace.DisposeAsync();

    [Fact]
    public async Task SessionsAndKeyRing_ShouldSurviveARestart_WhenBothLiveInPostgreSql()
    {
        var first = workspace.LocalHost();
        using (var client = BrandTestIdentity.Anonymous(first))
            await client.GetAsync("/health");
        var cookie = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-probe").Protect("kept");
        await first.Services.GetRequiredService<IDistributedCache>().SetStringAsync("session-probe", "kept");
        first.Services.GetRequiredService<IDistributedCache>().Should().BeOfType<PostgresDistributedCache>();
        first.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository.Should().BeOfType<PostgresXmlRepository>();
        await workspace.StopAsync(first);

        var restarted = workspace.LocalHost();
        using (var client = BrandTestIdentity.Anonymous(restarted))
            await client.GetAsync("/health");

        restarted.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-probe").Unprotect(cookie).Should().Be("kept");
        (await restarted.Services.GetRequiredService<IDistributedCache>().GetStringAsync("session-probe")).Should().Be("kept");
    }
}
