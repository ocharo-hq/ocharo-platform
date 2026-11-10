using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Infrastructure.Events.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Client;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2TokenExchange;
using WoW.Two.Sdk.Beta.Be.Testing;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>
/// Desk on WoW2 Events (<c>Events:BaseUrl</c> set, events-adoption.md #7, #8): the browser keeps calling Desk's routes,
/// an event accepted in Events shows on the page, tracking writes pass, the old fact writes answer 410, and a member
/// of one organization never reads another's events.
/// </summary>
public sealed class EventsServiceRouteTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    private const string EventsUrl = "https://events.example.test";

    [Fact]
    public async Task EventsPage_ShouldServeEvents_TrackThroughDesk_RefuseOldWrites_AndStayInsideItsOrganization()
    {
        var service = new FakeEventsService();
        EventModel accepted = service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["und"] = "Tashkent Fashion Week" },
            Kind = "runway",
            Place = new EventPlace { City = "Tashkent", Country = "UZ" },
            Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
        });
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        OrganizationAccess stranger = await fixture.CreateOwnerAsync();
        await using var host = Host(service);
        using HttpClient client = await fixture.LoginAsync(host, owner);
        using HttpClient strangerClient = await fixture.LoginAsync(host, stranger);
        string path = $"/api/organizations/{owner.OrganizationId}/events";

        JsonElement listed = (await DataAsync(await client.GetAsync(path))).EnumerateArray().Single();
        listed.GetProperty("id").GetGuid().Should().Be(accepted.Id);
        listed.GetProperty("status").GetString().Should().Be("candidate");
        listed.GetProperty("scope").GetString().Should().Be("local");
        listed.GetProperty("revision").GetInt32().Should().Be(1);

        JsonElement tracked = await DataAsync(await client.PutAsJsonAsync($"{path}/{accepted.Id}",
            Body(listed, 1, status: "shortlisted")));
        tracked.GetProperty("status").GetString().Should().Be("shortlisted");
        tracked.GetProperty("revision").GetInt32().Should().Be(2);
        service.TrackingWrites.Should().ContainSingle().Which.Stage.Should().Be("shortlisted");

        (await client.PutAsJsonAsync($"{path}/{accepted.Id}", Body(listed, 2, title: "Renamed"))).StatusCode
            .Should().Be(HttpStatusCode.Gone);
        (await client.DeleteAsync($"{path}/{accepted.Id}?expectedRevision=2")).StatusCode
            .Should().Be(HttpStatusCode.Gone);

        JsonElement proposed = await DataAsync(await client.PostAsJsonAsync(path, new
        {
            title = "Margilan Ikat Days", scope = "local", format = "festival", country = "UZ",
            datePrecision = "unknown", participation = "", notes = "",
        }));
        service.Event(proposed.GetProperty("id").GetGuid()).Review.Should().Be(EventReviewState.Candidate);

        (await strangerClient.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await strangerClient.PutAsJsonAsync($"{path}/{accepted.Id}", Body(listed, 2, status: "skipped"))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        service.TrackingWrites.Should().HaveCount(2);
    }

    [Fact]
    public async Task Host_ShouldExchangeTheSessionTokenForEvents_AndRefuseBundleImports()
    {
        await using var host = Host(service: null);
        using var anonymous = DeskTestHost.CreateClient(host);
        (await anonymous.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        host.Services.GetRequiredService<IOptions<EventsClientOptions>>().Value.BaseAddress.Should().Be(new Uri(EventsUrl));
        OAuth2TokenExchangeOptions exchange = host.Services.GetRequiredService<IOptionsMonitor<OAuth2TokenExchangeOptions>>()
            .Get(EventsClientConstants.HttpClientName);
        exchange.TokenEndpoint.Should().Be(new Uri("https://identity.example.test/connect/token"));
        exchange.ClientId.Should().Be("desk");
        exchange.Resource.Should().Be(EventsUrl + "/");
        host.Services.GetRequiredService<IOAuth2SubjectTokenSource>().GetType().FullName
            .Should().Be("WoW.Two.Sdk.Beta.Be.Identity.Bff.Sessions.BffSubjectTokenSource");
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IEventRepository>().Should().BeOfType<EventsServiceRepository>();
        scope.ServiceProvider.GetRequiredService<IEventRecordsReader>().Should().NotBeOfType<EventsServiceRepository>();
    }

    private WebApiTestHost<ApiAssemblyMarker> Host(FakeEventsService? service)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = fixture.ConnectionString,
            ["AllowedHosts"] = "localhost;127.0.0.1",
            ["Events:BaseUrl"] = EventsUrl,
            ["Identity:Client:ClientSecret"] = "test-secret",
        };
        var factory = new WebApiTestHost<ApiAssemblyMarker>
        {
            ConfigureHostHook = builder => builder.UseEnvironment(Environments.Development).UseIdentitySettings(),
            ConfigureServicesHook = services =>
            {
                DeskTestHost.Use(services);
                if (service is null) return;
                services.AddSingleton<ICatalogEventsBroker>(service);
                services.AddSingleton<IEventTrackingBroker>(service);
            },
            ConfigureConfigurationHook = builder => builder.AddInMemoryCollection(configuration)
        };
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        return factory;
    }

    private static object Body(JsonElement item, int expectedRevision, string? status = null, string? title = null) => new
    {
        expectedRevision,
        title = title ?? item.GetProperty("title").GetString(),
        scope = item.GetProperty("scope").GetString(),
        format = item.GetProperty("format").GetString(),
        country = item.GetProperty("country").GetString(),
        city = item.GetProperty("city").GetString(),
        startsOn = item.GetProperty("startsOn").GetString(),
        datePrecision = item.GetProperty("datePrecision").GetString(),
        participation = item.GetProperty("participation").GetString(),
        status = status ?? item.GetProperty("status").GetString(),
        priority = item.GetProperty("priority").GetString(),
        notes = item.GetProperty("notes").GetString(),
    };

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }
}
