using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api.Integrations;
using BrandWorkspace.Api.MediaProcessing;
using BrandWorkspace.Application.Access;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using SkiaSharp;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises delegated image HTTP boundaries with isolated authority and worker dependencies.</summary>
public sealed class MediaProcessingEndpointTests
{
    private const string Route = "/api/integrations/media/background-removal";
    private const int MaximumBytes = 20 * 1024 * 1024;

    [Theory]
    [InlineData("missing", false)]
    [InlineData("missing", true)]
    [InlineData("malformed", false)]
    [InlineData("malformed", true)]
    [InlineData("audience", false)]
    [InlineData("audience", true)]
    [InlineData("http", false)]
    [InlineData("http", true)]
    [InlineData("duplicate", false)]
    [InlineData("duplicate", true)]
    public async Task ImageEndpoint_ShouldReturn401_WhenTicketOrTransportIsInvalid(string scenario, bool process)
    {
        await using var host = await EndpointHost.CreateAsync();
        var ticket = scenario switch
        {
            "missing" => null,
            "malformed" => "not-a-ticket",
            "audience" => host.Issue("ocharo-studio"),
            _ => host.Issue()
        };
        var scheme = scenario == "http" ? "http" : "https";
        using var request = new HttpRequestMessage(process ? HttpMethod.Post : HttpMethod.Get,
            $"{scheme}://localhost{Route}{(process ? "" : "/capabilities")}");
        if (ticket is not null)
            request.Headers.TryAddWithoutValidation("X-Ocharo-Integration-Ticket",
                scenario == "duplicate" ? new[] { ticket, ticket } : new[] { ticket });
        if (process) request.Content = ImageContent(Png());

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        host.Worker.StatusCalls.Should().Be(0);
        host.Worker.RemoveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Processing_ShouldReturn403_WhenCurrentMembershipIsViewer()
    {
        await using var host = await EndpointHost.CreateAsync();
        var ticket = host.Issue();
        host.Access.Member = host.Access.Member! with { Role = "viewer", CanEdit = false, CanApprove = false };

        using var response = await host.PostAsync(ImageContent(Png()), ticket);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        host.Worker.RemoveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Capabilities_ShouldReportWorkerReadiness_WhenTicketHasReadAccess()
    {
        await using var host = await EndpointHost.CreateAsync();
        host.Access.Member = host.Access.Member! with { Role = "viewer", CanEdit = false, CanApprove = false };
        host.Worker.Status = new(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, Route + "/capabilities");
        request.Headers.Add("X-Ocharo-Integration-Ticket", host.Issue());

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        body.GetProperty("available").GetBoolean().Should().BeFalse();
        body.GetProperty("model").ValueKind.Should().Be(JsonValueKind.Null);
        host.Worker.StatusCalls.Should().Be(1);
        host.Worker.RemoveCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("ocharo-marketing")]
    [InlineData("ocharo-editorial")]
    public async Task Processing_ShouldReturnPngAndShapeHeaders_WhenCurrentEditorSendsValidImage(string audience)
    {
        await using var host = await EndpointHost.CreateAsync();
        var original = Png();

        using var response = await host.PostAsync(ImageContent(original), host.Issue(audience));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Image-Model").Should().Equal("fixture-u2net");
        response.Headers.GetValues("X-Image-Width").Should().Equal("3");
        response.Headers.GetValues("X-Image-Height").Should().Equal("2");
        host.Worker.LastSource.Should().Equal(original);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(host.Worker.Output);
        host.Worker.RemoveCalls.Should().Be(1);
        host.Access.MembershipChecks.Should().Be(2);
        host.Access.SessionChecks.Should().Be(2);
    }

    [Theory]
    [InlineData("session", HttpStatusCode.Unauthorized)]
    [InlineData("membership", HttpStatusCode.Unauthorized)]
    [InlineData("role", HttpStatusCode.Forbidden)]
    [InlineData("expiry", HttpStatusCode.Unauthorized)]
    public async Task Processing_ShouldRejectCompletedWork_WhenAuthorityChangesDuringProcessing(string revocation, HttpStatusCode expected)
    {
        await using var host = await EndpointHost.CreateAsync();
        host.Worker.Block = true;
        var pending = host.PostAsync(ImageContent(Png()));
        try
        {
            await host.Worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            switch (revocation)
            {
                case "session": host.Access.Active = false; break;
                case "membership": host.Access.Member = null; break;
                case "role": host.Access.Member = host.Access.Member! with { CanEdit = false, CanApprove = false, Role = "viewer" }; break;
                case "expiry": host.Clock.Advance(TimeSpan.FromMinutes(5)); break;
            }
        }
        finally { host.Worker.Release.TrySetResult(); }
        using var response = await pending;

        response.StatusCode.Should().Be(expected);
        response.Content.Headers.ContentType?.MediaType.Should().NotBe("image/png");
        response.Headers.Contains("X-Image-Model").Should().BeFalse();
        host.Worker.RemoveCalls.Should().Be(1);
    }

    [Theory]
    [InlineData("unsupported", HttpStatusCode.UnsupportedMediaType)]
    [InlineData("mismatch", HttpStatusCode.UnsupportedMediaType)]
    [InlineData("corrupt", HttpStatusCode.BadRequest)]
    public async Task Processing_ShouldRejectInput_WhenImageTypeOrBytesAreInvalid(string scenario, HttpStatusCode expected)
    {
        await using var host = await EndpointHost.CreateAsync();
        var bytes = scenario == "corrupt" ? Png()[..24] : Png();
        var type = scenario switch { "unsupported" => "image/svg+xml", "mismatch" => "image/jpeg", _ => "image/png" };

        using var response = await host.PostAsync(ImageContent(bytes, type));

        response.StatusCode.Should().Be(expected);
        host.Worker.RemoveCalls.Should().Be(0);
        // A rejected input must release the shared processing slot.
        using var retry = await host.PostAsync(ImageContent(Png()));
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Processing_ShouldReturn413_WhenEncodedBodyExceedsLimit(bool unknownLength)
    {
        await using var host = await EndpointHost.CreateAsync();
        var bytes = new byte[MaximumBytes + 1];
        using HttpContent content = unknownLength ? new UnknownLengthContent(bytes) : ImageContent(bytes);

        using var response = await host.PostAsync(content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        host.Worker.RemoveCalls.Should().Be(0);
        using var retry = await host.PostAsync(ImageContent(Png()));
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Processing_ShouldReturn429AndRecover_WhenAnotherOperationOwnsCapacity()
    {
        await using var host = await EndpointHost.CreateAsync();
        host.Worker.Block = true;
        var pending = host.PostAsync(ImageContent(Png()));
        try
        {
            await host.Worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var rejected = await host.PostAsync(ImageContent(Png()));
            rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            host.Worker.RemoveCalls.Should().Be(1);
        }
        finally { host.Worker.Release.TrySetResult(); }
        using var completed = await pending;
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        using var retry = await host.PostAsync(ImageContent(Png()));
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        host.Worker.RemoveCalls.Should().Be(2);
    }

    [Fact]
    public async Task Processing_ShouldReturn503AndRecover_WhenWorkerFails()
    {
        await using var host = await EndpointHost.CreateAsync();
        host.Worker.Failure = new("unavailable", "Image processing is unavailable.");
        using var failure = await host.PostAsync(ImageContent(Png()));
        failure.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        host.Worker.Failure = null;
        using var retry = await host.PostAsync(ImageContent(Png()));
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static ByteArrayContent ImageContent(byte[] bytes, string type = "image/png")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return content;
    }

    private static byte[] Png()
    {
        using var bitmap = new SKBitmap(3, 2, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(SKColors.Transparent);
        bitmap.SetPixel(1, 1, SKColors.Coral);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private sealed class EndpointHost : IAsyncDisposable
    {
        private const string Key = "isolated-media-endpoint-test-key-2026";
        private readonly WebApplication app;
        public FakeTimeProvider Clock { get; }
        public Access Access { get; }
        public Worker Worker { get; }
        public HttpClient Client { get; }
        private IntegrationTickets Tickets { get; }

        private EndpointHost(WebApplication application, FakeTimeProvider clock, Access access, Worker worker)
        {
            app = application; Clock = clock; Access = access; Worker = worker;
            Client = app.GetTestClient(); Client.BaseAddress = new Uri("https://localhost");
            Tickets = app.Services.GetRequiredService<IntegrationTickets>();
        }

        public static async Task<EndpointHost> CreateAsync()
        {
            var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero));
            var access = new Access(); var worker = new Worker();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Integrations:SigningKey"] = Key });
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddSingleton<IAccessRepository>(access);
            builder.Services.AddSingleton<IBackgroundRemovalService>(worker);
            builder.Services.AddSingleton<IntegrationTickets>();
            var application = builder.Build();
            application.MapMediaProcessingEndpoints();
            await application.StartAsync();
            return new(application, clock, access, worker);
        }

        public string Issue(string audience = "ocharo-marketing") =>
            JsonSerializer.SerializeToElement(Tickets.Issue(audience, Access.Member!, Access.Session)).GetProperty("token").GetString()!;

        public async Task<HttpResponseMessage> PostAsync(HttpContent content, string? ticket = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = content };
            request.Headers.Add("X-Ocharo-Integration-Ticket", ticket ?? Issue());
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Worker.Release.TrySetResult();
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class Worker : IBackgroundRemovalService
    {
        public BackgroundRemovalStatus Status { get; set; } = new(true, "fixture-u2net");
        public BackgroundRemovalException? Failure { get; set; }
        public bool Block { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public byte[] Output { get; } = Png();
        public byte[]? LastSource { get; private set; }
        public int StatusCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public Task<BackgroundRemovalStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            StatusCalls++;
            return Task.FromResult(Status);
        }
        public async Task<BackgroundRemovalResult> RemoveAsync(byte[] source, CancellationToken cancellationToken = default)
        {
            RemoveCalls++; LastSource = source.ToArray(); Entered.TrySetResult();
            if (Block) await Release.Task.WaitAsync(cancellationToken);
            if (Failure is { } failure) throw failure;
            return new(Output, 3, 2, "fixture-u2net");
        }
    }

    private sealed class Access : IAccessRepository
    {
        public Guid Organization { get; } = Guid.NewGuid();
        public Guid Session { get; } = Guid.NewGuid();
        public bool Active { get; set; } = true;
        public OrganizationAccess? Member { get; set; }
        public int SessionChecks { get; private set; }
        public int MembershipChecks { get; private set; }
        public Access() => Member = new(Organization, "media-test-operator", "owner", true, true);
        public Task<bool> IsSessionActiveAsync(Guid sessionId, string userId, DateTimeOffset now, CancellationToken token)
        {
            SessionChecks++;
            return Task.FromResult(Active && sessionId == Session && userId == "media-test-operator");
        }
        public Task<OrganizationAccess?> FindMembershipAsync(Guid organizationId, string userId, CancellationToken token)
        {
            MembershipChecks++;
            return Task.FromResult(organizationId == Organization && userId == "media-test-operator" ? Member : null);
        }
        public Task CreateSessionAsync(Guid sessionId, string userId, string securityStamp, DateTimeOffset expiresAt, Guid? replacedSessionId, CancellationToken token) => throw new NotSupportedException();
        public Task RevokeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<Organization>> ListOrganizationsAsync(string userId, CancellationToken token) => throw new NotSupportedException();
        public Task<Organization?> CreateOrganizationAsync(string name, string slug, string userId, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] bytes;
        public UnknownLengthContent(byte[] bytes) { this.bytes = bytes; Headers.ContentType = new("image/png"); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }
}
