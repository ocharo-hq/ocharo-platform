using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Configurations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;
using SkiaSharp;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Signals;
using WoW.Two.Sdk.Beta.Be.Identity.Testing;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises the image HTTP boundaries on Desk's own composition with no database: the policy, the bearer scheme and the
/// audience are Desk's, a <see cref="FakeTokenIssuer"/> signs the tokens, and a stand-in worker takes the images.</summary>
public sealed class MediaProcessingEndpointTests
{
    private const string Route = "/api/integrations/media/background-removal";
    private const int MaximumBytes = 20 * 1024 * 1024;
    private const string Caller = "ocharo-editorial";
    private const string Audience = "https://desk.test/";
    private const string Scope = "desk.remove-background";
    private const string ResourceSignalUrl = "https://desk.test/signals/identity";
    private const string BffSignalUrl = "https://desk.test/api/identity/signal";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageEndpoint_ShouldReturn401_WhenCallerHasNoToken(bool process)
    {
        await using var host = await EndpointHost.CreateAsync();

        using var response = await host.SendAsync(process, token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().ContainSingle(challenge => challenge.Scheme == "Bearer");
        host.Worker.StatusCalls.Should().Be(0);
        host.Worker.RemoveCalls.Should().Be(0);
    }

    [Fact]
    public async Task SignedSignal_ShouldEndTheSessionsTokens_AtTheResourceRoute()
    {
        await using var host = await EndpointHost.CreateAsync();
        var person = Person();
        var token = host.Tokens.IssuePersonToken(person, Audience, "ocharo-editorial", Scope);
        (await host.SendAsync(process: false, token)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var signal = await host.Client.SendAsync(host.Signals.SessionEnded(ResourceSignalUrl, person.SessionId!.Value.ToString("D"), person.Id.ToString("D")));

        signal.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await host.WaitUntilRefusedAsync(token)).Should().BeTrue("the handler runs off the request");
    }

    [Theory]
    [InlineData(ResourceSignalUrl)]
    [InlineData(BffSignalUrl)]
    public async Task SignalRoutes_ShouldAnswer401_ForAnUnsignedPostWithoutASession(string url)
    {
        await using var host = await EndpointHost.CreateAsync();

        using var response = await host.Client.PostAsync(new Uri(url).AbsolutePath, new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignedSignal_ShouldBeRefused_WhenItNamesAnotherServicesUrl()
    {
        await using var host = await EndpointHost.CreateAsync();
        var person = Person();
        using var captured = host.Signals.SessionEnded("https://elsewhere.test/signals/identity", person.SessionId!.Value.ToString("D"), person.Id.ToString("D"));
        captured.RequestUri = new Uri(new Uri(ResourceSignalUrl).AbsolutePath, UriKind.Relative);

        using var response = await host.Client.SendAsync(captured);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AccessChange_ShouldRefuseOnlyTokensIssuedBeforeIt()
    {
        await using var host = await EndpointHost.CreateAsync();
        var person = Person();
        var before = host.Tokens.IssuePersonToken(person, Audience, "ocharo-editorial", Scope);
        var change = host.Clock.GetUtcNow().AddSeconds(5);
        var denyList = host.Services.GetRequiredService<ITokenDenyList>();

        await denyList.AddAccessAsync(host.Tokens.Issuer, person.Id.ToString("D"), person.OrganizationId!.Value.ToString("D"), change, change.AddHours(1));
        host.Clock.SetUtcNow(change.AddSeconds(5));
        var after = host.Tokens.IssuePersonToken(person, Audience, "ocharo-editorial", Scope);

        (await host.SendAsync(process: false, before)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.SendAsync(process: false, after)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static FakePerson Person() => new() { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), SessionId = Guid.NewGuid() };

    [Theory]
    [InlineData("wrong-audience", "audience")]
    [InlineData("expired", "expired")]
    [InlineData("another-issuer", "Bearer")]
    public async Task ImageEndpoint_ShouldReturn401_WhenTokenIsRefused(string scenario, string reason)
    {
        await using var host = await EndpointHost.CreateAsync();
        using var stranger = new FakeTokenIssuer("https://another-issuer.test/");
        var token = scenario switch
        {
            "wrong-audience" => host.Tokens.IssueServiceToken(Caller, "https://elsewhere.test/", Scope),
            "expired" => host.ExpiredToken,
            _ => stranger.IssueServiceToken(Caller, Audience, Scope)
        };

        foreach (var process in new[] { false, true })
        {
            using var response = await host.SendAsync(process, token);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.ToString().Should().Contain(reason);
        }
        host.Worker.StatusCalls.Should().Be(0);
        host.Worker.RemoveCalls.Should().Be(0);
    }

    [Theory]
    [InlineData]
    [InlineData("desk.read")]
    [InlineData("files.write", "desk.remove-background-all")]
    public async Task ImageEndpoint_ShouldReturn403_WhenTokenLacksTheScope(params string[] scopes)
    {
        await using var host = await EndpointHost.CreateAsync();
        var token = host.Tokens.IssueServiceToken(Caller, Audience, scopes);

        foreach (var process in new[] { false, true })
        {
            using var response = await host.SendAsync(process, token);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        host.Worker.StatusCalls.Should().Be(0);
        host.Worker.RemoveCalls.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageEndpoint_ShouldReturn401_WhenSignedInOwnerHasNoToken(bool process)
    {
        await using var host = await EndpointHost.CreateAsync();
        var subject = Guid.NewGuid().ToString("D");
        DeskTestHost.Register(new DeskTestHost.Person(subject, Guid.NewGuid(), "owner", "Media owner", $"media-{Guid.NewGuid():N}@example.test"));
        host.Client.DefaultRequestHeaders.Add(DeskTestHost.PersonHeader, subject);

        using var response = await host.SendAsync(process, token: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        host.Worker.StatusCalls.Should().Be(0);
        host.Worker.RemoveCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("https://desk.test")]
    [InlineData("https://desk.test/")]
    public async Task ImageEndpoint_ShouldAcceptTheTokenAudience_HoweverTheResourceUriIsWritten(string resourceUri)
    {
        await using var host = await EndpointHost.CreateAsync(resourceUri);
        var bearer = host.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

        using var capabilities = await host.GetCapabilitiesAsync();
        using var processed = await host.PostAsync(ImageContent(Png()));

        bearer.TokenValidationParameters.ValidAudiences.Should().Equal(Audience);
        capabilities.StatusCode.Should().Be(HttpStatusCode.OK);
        processed.StatusCode.Should().Be(HttpStatusCode.OK);
        host.Worker.RemoveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Processing_ShouldNeedNoCsrfToken_WhenAServiceCallCarriesNoCookie()
    {
        await using var host = await EndpointHost.CreateAsync(standInSignIn: false);

        using var response = await host.SendAsync(process: true, host.ServiceToken());

        response.RequestMessage!.Headers.Contains("X-Csrf-Token").Should().BeFalse();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        host.Worker.RemoveCalls.Should().Be(1);
    }

    [Fact]
    public async Task ImageEndpoint_ShouldCheckTheIssuersOwnKeys_WhenTheAuthorityIsABareOrigin()
    {
        await using var identity = await LoopbackIdentity.StartAsync();
        await using var host = await EndpointHost.CreateAsync(loopbackAuthority: identity.Origin);
        using var impostor = new FakeTokenIssuer(identity.Tokens.Issuer);
        using var stranger = new FakeTokenIssuer("https://another-issuer.test/");
        var token = identity.Tokens.IssueServiceToken(Caller, Audience, Scope);

        using var first = await host.SendAsync(process: false, token);
        using var second = await host.SendAsync(process: true, token);
        var keyReads = identity.KeySetReads;
        using var foreign = await host.SendAsync(process: false, stranger.IssueServiceToken(Caller, Audience, Scope));
        using var forged = await host.SendAsync(process: false, impostor.IssueServiceToken(Caller, Audience, Scope));
        using var elsewhere = await host.SendAsync(process: false, identity.Tokens.IssueServiceToken(Caller, "https://elsewhere.test/", Scope));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        keyReads.Should().Be(1, "tokens are verified offline against the keys read once");
        foreign.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        forged.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        elsewhere.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BffCookie_ShouldStayEveryDefaultScheme_WithTheBearerSchemeRegistered()
    {
        await using var host = await EndpointHost.CreateAsync(standInSignIn: false);
        var schemes = host.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        (await schemes.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme)).Should().NotBeNull();
        (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name.Should().Be(BffAuthenticationSchemeConstants.Cookie);
        (await schemes.GetDefaultChallengeSchemeAsync())!.Name.Should().Be(BffAuthenticationSchemeConstants.Cookie);
        (await schemes.GetDefaultForbidSchemeAsync())!.Name.Should().Be(BffAuthenticationSchemeConstants.Cookie);
        (await schemes.GetDefaultSignInSchemeAsync())!.Name.Should().Be(BffAuthenticationSchemeConstants.Cookie);
        (await schemes.GetDefaultSignOutSchemeAsync())!.Name.Should().Be(BffAuthenticationSchemeConstants.Cookie);
    }

    [Theory]
    [InlineData("/api/organizations")]
    [InlineData("/api/organizations/00000000-0000-0000-0000-000000000001/events")]
    public async Task BrowserRoute_ShouldReturn401_WhenCallerHoldsOnlyAServiceToken(string route)
    {
        await using var host = await EndpointHost.CreateAsync(standInSignIn: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new("Bearer", host.ServiceToken());

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SessionRoute_ShouldReportSignedOut_WhenCallerHoldsOnlyAServiceToken()
    {
        await using var host = await EndpointHost.CreateAsync(standInSignIn: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/identity/session");
        request.Headers.Authorization = new("Bearer", host.ServiceToken());

        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        body.GetProperty("data").GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("desk.test")]
    [InlineData("/desk")]
    [InlineData("ftp://desk.test/")]
    [InlineData("http://desk.test/")]
    [InlineData("https://desk.test/#fragment")]
    public async Task Boot_ShouldRefuse_WhenResourceUriIsMissingOrMalformed(string? resourceUri)
    {
        var start = () => EndpointHost.CreateAsync(resourceUri);

        (await start.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain("Identity:Resource:Uri");
    }

    [Fact]
    public async Task ResourceUri_ShouldComeFromTheDeploymentEnvironment()
    {
        const string variable = "Identity__Resource__Uri";
        Environment.SetEnvironmentVariable(variable, "https://from-environment.test");
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = DeskTestHost.ApplicationName, EnvironmentName = Environments.Development, ContentRootPath = AppContext.BaseDirectory
            });
            builder.Configure();
            await using var app = builder.Build();

            app.Services.GetRequiredService<IdentityResourceSettings>().Uri.Should().Be("https://from-environment.test");
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [Fact]
    public async Task Worker_ShouldBeAskedAtItsPlatformName_WithTheDeployedSettings()
    {
        const string key = "a-private-worker-key-for-this-test-only";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = DeskTestHost.ApplicationName, EnvironmentName = Environments.Development, ContentRootPath = AppContext.BaseDirectory,
            Args = ["--Identity:Resource:Uri=https://desk.test", "--ImageProcessing:WorkerBaseUrl=http://ocharo-dev-image-worker:8080", $"--ImageProcessing:WorkerKey={key}"]
        });
        builder.Configure();
        var worker = new WorkerStandIn();
        builder.Services.AddHttpClient<IBackgroundRemovalService, HttpBackgroundRemovalService>().ConfigurePrimaryHttpMessageHandler(() => worker);
        await using var app = builder.Build();

        var status = await app.Services.GetRequiredService<IBackgroundRemovalService>().GetStatusAsync();

        status.Should().Be(new BackgroundRemovalStatus(true, "u2net"));
        worker.Asked.Should().Be("http://ocharo-dev-image-worker:8080/health/ready");
        worker.Key.Should().Be(key);
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
        private readonly WebApplication app;
        public Worker Worker { get; }
        public HttpClient Client { get; }
        public FakeTokenIssuer Tokens { get; }
        public FakeTimeProvider Clock { get; }
        public IdentitySignalSender Signals { get; }

        /// <summary>A token of <see cref="Tokens"/> that expired an hour ago, so only its lifetime refuses it.</summary>
        public string ExpiredToken { get; }

        public IServiceProvider Services => app.Services;

        private EndpointHost(WebApplication application, Worker worker, FakeTokenIssuer tokens, string expiredToken, FakeTimeProvider clock, IdentitySignalSender signals)
        {
            app = application; Worker = worker; Tokens = tokens; ExpiredToken = expiredToken; Clock = clock; Signals = signals;
            Client = app.GetTestClient(); Client.BaseAddress = new Uri("https://localhost");
        }

        /// <summary>Starts Desk with no database. <paramref name="standInSignIn"/> swaps the BFF cookie for <see cref="DeskTestHost"/>'s
        /// handler; <paramref name="loopbackAuthority"/> leaves the real verification on, reading that instance's keys.</summary>
        public static async Task<EndpointHost> CreateAsync(string? resourceUri = "https://desk.test", bool standInSignIn = true, string? loopbackAuthority = null)
        {
            var worker = new Worker();
            var clock = new FakeTimeProvider(DateTimeOffset.UtcNow.AddHours(-1));
            var tokens = new FakeTokenIssuer(clock: clock);
            var expiredToken = tokens.IssueServiceToken(Caller, Audience, Scope);
            clock.SetUtcNow(DateTimeOffset.UtcNow);
            string[] args =
            [
                "--AllowedHosts=localhost",
                "--ConnectionStrings:Database=Host=127.0.0.1;Port=1;Database=unavailable;Username=unused;Timeout=1",
                $"--Identity:Client:Authority={loopbackAuthority ?? DeskTestHost.IdentityClientSettings["Identity:Client:Authority"]}",
                "--Identity:Client:ClientId=desk",
                $"--Identity:Client:IsHttpsMetadataRequired={loopbackAuthority is null}",
                $"--Identity:Client:SignalUrl={BffSignalUrl}",
                $"--Identity:Resource:SignalUrl={ResourceSignalUrl}",
                .. resourceUri is null ? Array.Empty<string>() : [$"--Identity:Resource:Uri={resourceUri}"]
            ];
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = DeskTestHost.ApplicationName, Args = args, EnvironmentName = Environments.Development, ContentRootPath = AppContext.BaseDirectory
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Configure();
            builder.Services.AddSingleton<IBackgroundRemovalService>(worker);
            if (standInSignIn) DeskTestHost.Use(builder.Services);
            if (loopbackAuthority is null) builder.Services.AddFakeTokenIssuer(tokens);
            // Desk's own cache is a table, and this host has no database; only a person's token reads the deny list.
            builder.Services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));
            var signals = new IdentitySignalSender(tokens.Issuer);
            builder.Services.PostConfigure<IdentitySignalReceiverOptions>(options => options.TrustedIssuers.Add(signals.TrustedIssuer));
            var application = builder.Build();
            application.Configure();
            try { await application.StartAsync(); }
            catch { await application.DisposeAsync(); tokens.Dispose(); signals.Dispose(); throw; }
            return new(application, worker, tokens, expiredToken, clock, signals);
        }

        public string ServiceToken() => Tokens.IssueServiceToken(Caller, Audience, Scope);

        /// <summary>Asks until the token is refused, for up to 5 seconds.</summary>
        public async Task<bool> WaitUntilRefusedAsync(string token)
        {
            for (var attempt = 0; attempt < 100; attempt++)
            {
                using var response = await SendAsync(process: false, token);
                if (response.StatusCode == HttpStatusCode.Unauthorized) return true;
                await Task.Delay(50);
            }
            return false;
        }

        public Task<HttpResponseMessage> GetCapabilitiesAsync() => SendAsync(process: false, ServiceToken());

        public async Task<HttpResponseMessage> PostAsync(HttpContent content)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = content };
            request.Headers.Authorization = new("Bearer", ServiceToken());
            return await Client.SendAsync(request);
        }

        public async Task<HttpResponseMessage> SendAsync(bool process, string? token)
        {
            using var request = new HttpRequestMessage(process ? HttpMethod.Post : HttpMethod.Get, Route + (process ? "" : "/capabilities"));
            if (process) request.Content = ImageContent(Png());
            if (token is not null) request.Headers.Authorization = new("Bearer", token);
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Worker.Release.TrySetResult();
            Client.Dispose();
            await app.DisposeAsync();
            Tokens.Dispose();
        }
    }

    /// <summary>The documents a real Identity instance serves for its issuer, over loopback HTTP, signing through a <see cref="FakeTokenIssuer"/>.</summary>
    private sealed class LoopbackIdentity : IAsyncDisposable
    {
        private readonly WebApplication app;
        private int keySetReads;
        public string Origin { get; private set; } = "";
        public FakeTokenIssuer Tokens { get; private set; } = null!;
        public int KeySetReads => Volatile.Read(ref keySetReads);

        private LoopbackIdentity(WebApplication application) => app = application;

        public static async Task<LoopbackIdentity> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            var identity = new LoopbackIdentity(builder.Build());
            identity.app.MapGet("/.well-known/openid-configuration", () => Results.Json(new { issuer = identity.Tokens.Issuer, jwks_uri = identity.Origin + "/keys" }));
            identity.app.MapGet("/keys", () =>
            {
                Interlocked.Increment(ref identity.keySetReads);
                var key = JsonWebKeyConverter.ConvertFromRSASecurityKey((RsaSecurityKey)identity.Tokens.AsTrustedIssuer().SigningKeys![0]);
                return Results.Json(new { keys = new[] { new { kty = key.Kty, use = "sig", alg = SecurityAlgorithms.RsaSha256, kid = key.Kid, n = key.N, e = key.E } } });
            });
            await identity.app.StartAsync();
            identity.Origin = identity.app.Urls.Single().TrimEnd('/');
            identity.Tokens = new FakeTokenIssuer(identity.Origin + "/");
            return identity;
        }

        public async ValueTask DisposeAsync()
        {
            await app.DisposeAsync();
            Tokens.Dispose();
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

    /// <summary>Answers as a ready worker and records what Desk's real client sent.</summary>
    private sealed class WorkerStandIn : HttpMessageHandler
    {
        public string? Asked { get; private set; }
        public string? Key { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request.RequestUri!.AbsoluteUri;
            Key = request.Headers.GetValues("X-Image-Worker-Key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"ready":true,"model":"u2net"}""") });
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
