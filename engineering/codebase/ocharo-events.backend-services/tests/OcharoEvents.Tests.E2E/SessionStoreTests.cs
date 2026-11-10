using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OcharoEvents.Persistence;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;

namespace OcharoEvents.Tests.E2E;

/// <summary>Sign-in sessions and the key ring that encrypts their cookies live in the product database (events-app.md #2),
/// so a restarted or replaced host reads what the one before it wrote and nobody is signed out.</summary>
public sealed class SessionStoreTests
{
    [Fact]
    public async Task SessionsAndKeyRing_ShouldSurviveARestart_WhenBothLiveInPostgreSql()
    {
        var database = await EventsTestDatabase.CreateAsync();
        string cookie;
        await using (var first = EventsHost.Create(new Dictionary<string, string?>(), database: database))
        {
            using (var client = EventsTestIdentity.Anonymous(first.Factory))
                await client.GetAsync("/health");
            var services = first.Factory.Services;
            cookie = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-probe").Protect("kept");
            await services.GetRequiredService<IDistributedCache>().SetStringAsync("session-probe", "kept");
            services.GetRequiredService<IDistributedCache>().Should().BeOfType<PostgresDistributedCache>();
            services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository.Should().BeOfType<PostgresXmlRepository>();
        }

        await using var restarted = EventsHost.Create(new Dictionary<string, string?>(), database: database);
        using (var client = EventsTestIdentity.Anonymous(restarted.Factory))
            await client.GetAsync("/health");

        restarted.Factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-probe").Unprotect(cookie).Should().Be("kept");
        (await restarted.Factory.Services.GetRequiredService<IDistributedCache>().GetStringAsync("session-probe")).Should().Be("kept");
    }
}
