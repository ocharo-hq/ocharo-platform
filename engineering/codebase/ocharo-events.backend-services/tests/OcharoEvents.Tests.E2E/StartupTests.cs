using System.Net;

namespace OcharoEvents.Tests.E2E;

/// <summary>The service keeps no events store (events-app.md #2): a deployed host's config check refuses the boot without
/// <c>Events:BaseUrl</c> or its database, and the host boots with them; a local run boots without Events and answers 503
/// on the Events routes (<see cref="EventsServiceRouteTests"/>).</summary>
public sealed class StartupTests
{
    [Theory]
    [InlineData("Development", "dev")]
    [InlineData("Production", "prod")]
    public async Task DeployedHost_ShouldRefuseToBoot_WhenEventsBaseUrlIsMissing(string environment, string deployEnvironment)
    {
        var logs = new CapturedLogs();
        await using var host = EventsHost.Create(
            new Dictionary<string, string?> { ["DEPLOY_ENVIRONMENT"] = deployEnvironment, ["Events:BaseUrl"] = null },
            environment: environment,
            logs: logs);

        var failure = Record.Exception(() => EventsTestIdentity.Anonymous(host.Factory));

        failure.Should().NotBeNull();
        var log = string.Join("\n", logs.Entries.Select(entry => entry.Message));
        log.Should().Contain("Events:BaseUrl is required").And.Contain("The deployed boot refuses to serve");
    }

    [Theory]
    [InlineData("Development", "dev")]
    [InlineData("Production", "prod")]
    public async Task DeployedHost_ShouldBoot_WhenEventsBaseUrlIsSet(string environment, string deployEnvironment)
    {
        await using var host = EventsHost.Create(
            new Dictionary<string, string?> { ["DEPLOY_ENVIRONMENT"] = deployEnvironment },
            environment: environment);
        using var client = EventsTestIdentity.Anonymous(host.Factory);

        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeployedHost_ShouldRefuseToBoot_WhenTheDatabaseIsMissing()
    {
        var logs = new CapturedLogs();
        await using var host = EventsHost.Create(
            new Dictionary<string, string?> { ["DEPLOY_ENVIRONMENT"] = "prod", ["ConnectionStrings:Database"] = null },
            logs: logs);

        var failure = Record.Exception(() => EventsTestIdentity.Anonymous(host.Factory));

        failure.Should().NotBeNull();
        var log = string.Join("\n", logs.Entries.Select(entry => entry.Message));
        log.Should().Contain("ConnectionString is required").And.Contain("The deployed boot refuses to serve");
    }
}
