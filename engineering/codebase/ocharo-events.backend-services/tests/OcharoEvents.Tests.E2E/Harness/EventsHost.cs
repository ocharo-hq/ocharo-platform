using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Files.Urls;

namespace OcharoEvents.Tests.Harness;

/// <summary>An isolated Events host with its own product database, signed in through <see cref="EventsTestIdentity"/>;
/// with a <see cref="RecordingEventsBrokers"/>, the brokers the Events page's mirror reaches record each call and answer
/// what the test set, with a <see cref="FakeDigestsBroker"/> WoW2 Events' digests answer from memory, and with a
/// <see cref="FakeFileUrlsBroker"/> WoW2 Files' signed URLs answer from a fake content host. <c>settings</c> apply last, so
/// a test may change or clear (null) any default.</summary>
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
        RecordingEventsBrokers? events = null,
        string environment = "Testing",
        string? database = null,
        CapturedLogs? logs = null,
        FakeDigestsBroker? digests = null,
        FakeFileUrlsBroker? files = null)
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
                if (digests is not null) services.AddSingleton<IEventDigestsBroker>(digests);
                if (files is not null) services.AddSingleton<IFileUrlsBroker>(files);
                if (events is null) return;
                services.AddSingleton<IEventTrackingBroker>(events);
                services.AddSingleton<ICatalogEventsBroker>(events);
                services.AddSingleton<IEventWorkflowsBroker>(events);
                services.AddSingleton<IEventCalendarBroker>(events);
                services.AddSingleton<IEventIntakeBroker>(events);
            });
        });
        return new EventsHost(factory);
    }

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
