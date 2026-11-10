using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;

namespace OcharoEvents.Tests.Harness;

/// <summary>An isolated Events host with its own product database, signed in through <see cref="EventsTestIdentity"/>;
/// with a <see cref="FakeEventsService"/>, WoW2 Events' two brokers answer from memory. <c>settings</c> apply last, so a
/// test may change or clear (null) any default.</summary>
internal sealed class EventsHost : IAsyncDisposable
{
    /// <summary>The WoW2 Events address the test hosts are configured with; the fake brokers answer for it.</summary>
    public const string EventsUrl = "https://events.example.test/";

    private EventsHost(WebApplicationFactory<Program> factory) => Factory = factory;

    public WebApplicationFactory<Program> Factory { get; }

    /// <summary>Builds a host in <paramref name="environment"/>; <c>Testing</c> boots like a deployed host, <c>Development</c>
    /// without <c>DEPLOY_ENVIRONMENT</c> like a local run.</summary>
    public static EventsHost Create(
        IReadOnlyDictionary<string, string?> settings,
        FakeEventsService? events = null,
        string environment = "Testing",
        string? database = null,
        CapturedLogs? logs = null)
    {
        var configuration = EventsTestIdentity.Settings(database);
        foreach (var (key, value) in settings)
            configuration[key] = value;
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            builder.ConfigureServices(services =>
            {
                EventsTestIdentity.Use(services);
                if (logs is not null) services.AddSingleton<ILogEventSink>(logs);
            });
            builder.ConfigureTestServices(services =>
            {
                if (events is null) return;
                services.AddSingleton<ICatalogEventsBroker>(events);
                services.AddSingleton<IEventTrackingBroker>(events);
            });
        });
        return new EventsHost(factory);
    }

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
