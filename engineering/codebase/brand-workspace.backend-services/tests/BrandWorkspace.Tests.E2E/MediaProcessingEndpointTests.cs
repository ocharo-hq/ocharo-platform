using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api.MediaProcessing;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises the image HTTP boundaries with a test caller standing in for the closed policy, and the closed policy itself on the Desk host.</summary>
public sealed class MediaProcessingEndpointTests
{
    private const string Route = "/api/integrations/media/background-removal";
    private const int MaximumBytes = 20 * 1024 * 1024;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageEndpoint_ShouldReturn401_WhenCallerIsAnonymous(bool process)
    {
        await using var host = await EndpointHost.CreateAsync();
        using var request = new HttpRequestMessage(process ? HttpMethod.Post : HttpMethod.Get, Route + (process ? "" : "/capabilities"));
        if (process) request.Content = ImageContent(Png());

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        host.Worker.StatusCalls.Should().Be(0);
        host.Worker.RemoveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Capabilities_ShouldReportWorkerReadiness_WhenCallerIsAuthorized()
    {
        await using var host = await EndpointHost.CreateAsync();
        host.Worker.Status = new(false);

        using var response = await host.GetCapabilitiesAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        body.GetProperty("available").GetBoolean().Should().BeFalse();
        body.GetProperty("model").ValueKind.Should().Be(JsonValueKind.Null);
        host.Worker.StatusCalls.Should().Be(1);
        host.Worker.RemoveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Processing_ShouldReturnPngAndShapeHeaders_WhenCallerSendsValidImage()
    {
        await using var host = await EndpointHost.CreateAsync();
        var original = Png();

        using var response = await host.PostAsync(ImageContent(original));

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
        private const string Scheme = "MediaTest";
        private const string CallerHeader = "X-Test-Caller";
        private readonly WebApplication app;
        public Worker Worker { get; }
        public HttpClient Client { get; }

        private EndpointHost(WebApplication application, Worker worker)
        {
            app = application; Worker = worker;
            Client = app.GetTestClient(); Client.BaseAddress = new Uri("https://localhost");
        }

        public static async Task<EndpointHost> CreateAsync()
        {
            var worker = new Worker();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = DeskTestHost.ApplicationName, EnvironmentName = Environments.Development });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<IBackgroundRemovalService>(worker);
            // The test host replaces the closed policy with one that admits the authenticated test caller.
            builder.Services.AddAuthentication(Scheme).AddScheme<AuthenticationSchemeOptions, CallerHandler>(Scheme, null);
            builder.Services.AddAuthorizationBuilder().AddPolicy(MediaProcessingEndpoints.Policy, policy => policy
                .AddAuthenticationSchemes(Scheme).RequireAuthenticatedUser());
            var application = builder.Build();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapMediaProcessingEndpoints();
            await application.StartAsync();
            return new(application, worker);
        }

        public async Task<HttpResponseMessage> GetCapabilitiesAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Route + "/capabilities");
            request.Headers.Add(CallerHeader, "caller");
            return await Client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> PostAsync(HttpContent content)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = content };
            request.Headers.Add(CallerHeader, "caller");
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Worker.Release.TrySetResult();
            Client.Dispose();
            await app.DisposeAsync();
        }

        private sealed class CallerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
        {
            protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
                Task.FromResult(Request.Headers.ContainsKey(CallerHeader)
                    ? AuthenticateResult.Success(new AuthenticationTicket(
                        new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "caller")], Scheme.Name)), Scheme.Name))
                    : AuthenticateResult.NoResult());
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

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] bytes;
        public UnknownLengthContent(byte[] bytes) { this.bytes = bytes; Headers.ContentType = new("image/png"); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }
}

/// <summary>The registered policy keeps both routes shut to every caller, whatever the Desk membership.</summary>
public sealed class MediaProcessingPolicyTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    private const string Route = "/api/integrations/media/background-removal";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageEndpoint_ShouldReturn401_WhenAnonymous(bool process)
    {
        using var client = DeskTestHost.CreateClient(fixture.Factory);

        using var response = await Send(client, process);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageEndpoint_ShouldReturn403_WhenSignedInMemberCalls(bool process)
    {
        var organization = Guid.NewGuid();
        var subject = Guid.NewGuid().ToString("D");
        DeskTestHost.Register(new DeskTestHost.Person(subject, organization, "owner", "Media owner", $"media-{Guid.NewGuid():N}@example.test"));
        using var client = DeskTestHost.CreateClient(fixture.Factory, subject);

        using var response = await Send(client, process);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, bool process)
    {
        if (!process) return client.GetAsync(Route + "/capabilities");
        var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return client.PostAsync(Route, content);
    }
}
