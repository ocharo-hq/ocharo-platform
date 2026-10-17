using BrandWorkspace.Api.Identity.Telegram;
using Telegram.Bot;
using WoW.Two.Sdk.Beta.Be.Identity.Otp;
using WoW.Two.Sdk.Beta.Be.Identity.Otp.Telegram;
using WoW.Two.Sdk.Beta.Be.Foundation.Options;
using System.Threading.RateLimiting;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;
using BrandWorkspace.Api.MediaProcessing;
using BrandWorkspace.Api.Commerce;
using BrandWorkspace.Api.Integrations;
using BrandWorkspace.Api.DesignDocuments;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Foundation;
using BrandWorkspace.Infrastructure.Preparation.Services;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Application.Preparation.Services;
using BrandWorkspace.Persistence.Preparation.Repositories;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using BrandWorkspace.Persistence.DesignDocuments.Repositories;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Infrastructure.Commerce;
using BrandWorkspace.Persistence.Commerce;
using BrandWorkspace.Api.Sourcing;
using BrandWorkspace.Application.Sourcing.Repositories;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using BrandWorkspace.Persistence.Sourcing.Repositories;
using BrandWorkspace.Api.Events;
using BrandWorkspace.Api.Mcp;
using WoW.Two.Sdk.Beta.Be.Ai.Mcp;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Infrastructure.Events.Services;
using BrandWorkspace.Persistence.Events.Repositories;
using WoW.Two.Sdk.Beta.Be.Media.Images;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using BrandWorkspace.Api.Preparation;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Identity.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.Cookies;
using System.Data.Common;
using BrandWorkspace.Application.Foundation.Repositories;
using BrandWorkspace.Domain.Foundation.Models;
using BrandWorkspace.Persistence.Foundation.Repositories;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers the permitted deployment environment inputs.</summary>
    private static WebApplicationBuilder AddExplicitEnvironment(this WebApplicationBuilder builder)
    {
        foreach (var source in builder.Configuration.Sources.OfType<EnvironmentVariablesConfigurationSource>().ToArray())
            builder.Configuration.Sources.Remove(source);
        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
        var values = new Dictionary<string, string?>();
        if (Environment.GetEnvironmentVariable("ConnectionStrings__Database") is { } connection)
            values["ConnectionStrings:Database"] = connection;
        if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is { } urls) values["urls"] = urls;
        if (Environment.GetEnvironmentVariable("AllowedHosts") is { } allowedHosts) values["AllowedHosts"] = allowedHosts;
        foreach (var key in new[] { "ImageProcessing__WorkerBaseUrl", "ImageProcessing__WorkerKey", "Integrations__SigningKey", "Commerce__SourceRoot", "Commerce__AdoptionOrganizationId", "DataProtection__KeysPath", "DataProtection__ApplicationName",
                     "Kestrel__Certificates__Default__Path", "Kestrel__Certificates__Default__Password" })
            if (Environment.GetEnvironmentVariable(key) is { } value) values[key.Replace("__", ":")] = value;
        if (Environment.GetEnvironmentVariable("MCP_ENABLED") is { } mcpEnabled) values["Mcp:Enabled"] = mcpEnabled;
        foreach (var (alias, name) in new[] { ("TELEGRAM_AUTH_ENABLED", "Enabled"), ("TELEGRAM_BOT_USERNAME", "BotUsername"),
                     ("TELEGRAM_BOT_TOKEN", "BotToken"), ("TELEGRAM_WEBHOOK_SECRET", "WebhookSecret"), ("TELEGRAM_POLLING_ENABLED", "PollingEnabled") })
            if (Environment.GetEnvironmentVariable(alias) is { } value) values[$"{TelegramAuthSettings.SectionName}:{name}"] = value;
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }

    /// <summary>Registers the PostgreSQL connection pool and SDK connection factory.</summary>
    private static WebApplicationBuilder AddPostgresDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(services => NpgsqlDataSource.Create(
            services.GetRequiredService<IConfiguration>().GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));
        builder.Services.AddSingleton<DbDataSource>(services => services.GetRequiredService<NpgsqlDataSource>());
        builder.Services.AddDataSourceConnectionFactory();
        return builder;
    }

    /// <summary>Registers foundation diagnostics and migration services for the explicit CLI invocation.</summary>
    private static WebApplicationBuilder AddFoundation(this WebApplicationBuilder builder, bool migrate)
    {
        builder.Services.AddSingleton<IFoundationRepository, FoundationRepository>();
        builder.Services.AddSingleton(TimeProvider.System);
        if (!migrate) return builder;
        builder.Services.AddDatabaseBespokeMigrations(typeof(FoundationRepository).Assembly, options =>
        {
            options.Version = BuildIdentity.ProductVersion;
            options.AdvisoryLockId = 8_264_001;
        });
        builder.Services.AddHangfire(configuration => configuration.UsePostgreSqlStorage(
            storage => storage.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Database")),
            new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true }));
        return builder;
    }

    /// <summary>Registers framework identity, durable sessions, and the published SDK cookie/default-deny contracts.</summary>
    private static WebApplicationBuilder AddAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AccessDbContext>((services, options) =>
            options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));
        builder.Services.AddIdentityCore<AccessUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<AccessDbContext>().AddSignInManager();
        builder.Services.AddSingleton<IAccessRepository, AccessRepository>();
        builder.Services.AddScoped<OrganizationAccessFilter>();
        builder.Services.AddCookieAuthentication(options =>
        {
            options.CookieName = "__Host-brand-workspace-session";
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Mode = AuthChallengeMode.Api;
        });
        builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Path = "/";
            options.Events.OnValidatePrincipal = async context =>
            {
                var session = AccessClaims.Session(context.Principal!);
                var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var services = context.HttpContext.RequestServices;
                if (session is null || userId is null || !await services.GetRequiredService<IAccessRepository>()
                    .IsSessionActiveAsync(session.Value, userId, services.GetRequiredService<TimeProvider>().GetUtcNow(), context.HttpContext.RequestAborted))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            };
            options.Events.OnRedirectToLogin = context =>
                AccessProblems.Create(401, "Authentication required", "authentication_required").ExecuteAsync(context.HttpContext);
            options.Events.OnRedirectToAccessDenied = context =>
                AccessProblems.Create(403, "Access denied", "forbidden").ExecuteAsync(context.HttpContext);
        });
        builder.Services.AddDefaultDenyAuthorization(CookieAuthenticationDefaults.AuthenticationScheme);
        builder.Services.AddSingleton<IAntiforgeryAdditionalDataProvider, SessionAntiforgeryDataProvider>();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-brand-workspace-antiforgery";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        // Resolve final configuration after test/deployment overrides have been applied.
        builder.Services.AddDataProtection();
        builder.Services.AddOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>()
            .Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            {
                var path = configuration["DataProtection:KeysPath"];
                if (string.IsNullOrWhiteSpace(path))
                {
                    if (!environment.IsDevelopment()) throw new InvalidOperationException("DataProtection:KeysPath must name durable storage.");
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "brand-workspace", environment.EnvironmentName, "keys");
                }
                options.XmlRepository = new Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository(
                    new DirectoryInfo(path), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            });
        builder.Services.AddOptions<DataProtectionOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            options.ApplicationDiscriminator = configuration["DataProtection:ApplicationName"] ?? $"brand-workspace:{environment.EnvironmentName}");
        return builder;
    }

    /// <summary>Registers opt-in bot account linking, transactional OTP verification and Development-only polling.</summary>
    private static WebApplicationBuilder AddTelegramAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddValidatedSettings<TelegramAuthSettings>(builder.Configuration, TelegramAuthSettings.SectionName,
            options => options.Validate(value => !value.Enabled ||
                    (System.Text.RegularExpressions.Regex.IsMatch(value.BotUsername, "^[A-Za-z][A-Za-z0-9_]{4,31}$")
                     && System.Text.RegularExpressions.Regex.IsMatch(value.BotToken, "^[0-9]+:[A-Za-z0-9_-]{30,}$")
                     && (value.PollingEnabled || System.Text.RegularExpressions.Regex.IsMatch(value.WebhookSecret, "^[A-Za-z0-9_-]{32,256}$"))),
                    "Enabled Telegram sign-in requires a bot username, server token, and webhook secret unless Development polling is selected.")
                .Validate(value => !value.PollingEnabled || builder.Environment.IsDevelopment(), "Telegram polling is allowed only in Development."));
        builder.Services.AddScoped<TelegramAuthRepository>();
        builder.Services.AddScoped<IOtpRepository>(services => services.GetRequiredService<TelegramAuthRepository>());
        builder.Services.AddOtpService();
        builder.Services.AddTelegramOtpDelivery(options => options.ScopeDisplayNames[TelegramAuthEndpoints.Scope] = "Ocharo");
        builder.Services.PostConfigure<OtpOptions>(options =>
        {
            options.CodeLength = 6;
            options.CodeKind = OtpCodeKind.Numeric;
            options.CodeLifetime = TimeSpan.FromMinutes(5);
            options.RateLimitWindow = TimeSpan.FromSeconds(30);
            options.MaxAttempts = 5;
        });
        builder.Services.AddSingleton<ITelegramBotClient>(services => new TelegramBotClient(services.GetRequiredService<TelegramAuthSettings>().BotToken));
        builder.Services.AddScoped<TelegramBotUpdatesHandler>();
        builder.Services.AddHostedService<TelegramPollingService>();
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy(TelegramAuthEndpoints.RatePolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await AccessProblems.Create(429, "Wait before trying again", "telegram_rate_limited").ExecuteAsync(context.HttpContext);
            };
        });
        return builder;
    }

    /// <summary>Registers the organization-scoped content preparation workflow.</summary>
    private static WebApplicationBuilder AddPreparation(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IPreparationRepository, PreparationRepository>();
        builder.Services.AddSingleton<IManualPackageBuilder, ManualPackageBuilder>();
        builder.Services.AddSingleton<ImageValidationService>();
        builder.Services.AddScoped<PreparationService>();
        return builder;
    }

    /// <summary>Registers durable editor documents and their validation workflow.</summary>
    private static WebApplicationBuilder AddDesignDocuments(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IDesignDocumentRepository, DesignDocumentRepository>();
        builder.Services.AddSingleton<DesignDocumentValidationService>();
        builder.Services.AddScoped<DesignDocumentService>();
        return builder;
    }

    /// <summary>Registers scoped integration ticket verification.</summary>
    private static WebApplicationBuilder AddIntegrations(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IntegrationTickets>();
        return builder;
    }

    /// <summary>Registers commerce persistence and preserved source adoption.</summary>
    private static WebApplicationBuilder AddCommerce(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ICommerceRepository, CommerceRepository>();
        builder.Services.AddSingleton<LegacyCommerceReader>();
        return builder;
    }

    /// <summary>Registers supplier records, contact routes and bounded content-addressed supplier media.</summary>
    private static WebApplicationBuilder AddSourcing(this WebApplicationBuilder builder)
    {
        builder.Services.AddImageProcessing(options =>
        {
            options.MaxInputBytes = SourcingMediaInspector.MaximumImageBytes;
            options.MaxPixels = SourcingMediaInspector.MaximumPixels;
        });
        builder.Services.AddSingleton<ISourcingRepository, SourcingRepository>();
        builder.Services.AddSingleton<SourcingValidationService>();
        builder.Services.AddSingleton<SourcingMediaInspector>();
        builder.Services.AddScoped<SourcingService>();
        builder.Services.AddScoped<SourcingImportService>();
        return builder;
    }

    /// <summary>Registers the showcase events feed and its explicit bundle import.</summary>
    private static WebApplicationBuilder AddEvents(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IEventRepository, EventRepository>();
        builder.Services.AddSingleton<EventValidationService>();
        builder.Services.AddScoped<EventService>();
        builder.Services.AddScoped<EventImportService>();
        return builder;
    }

    /// <summary>Registers the optional image worker from explicit server configuration.</summary>
    private static WebApplicationBuilder AddMediaProcessing(this WebApplicationBuilder builder)
    {
        builder.Services.AddBackgroundRemoval(options =>
        {
            options.BaseUrl = builder.Configuration["ImageProcessing:WorkerBaseUrl"];
            options.ApiKey = builder.Configuration["ImageProcessing:WorkerKey"];
        });
        return builder;
    }

    /// <summary>Registers opt-in read-only MCP with audience-bound delegated tickets.</summary>
    private static WebApplicationBuilder AddDeskMcp(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(services => new DeskMcpSettings(
            services.GetRequiredService<IConfiguration>().GetValue<bool>("Mcp:Enabled")));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, DeskMcpAuthenticationHandler>(
            DeskMcpAuthenticationHandler.SchemeName, null);
        builder.Services.AddAuthorizationBuilder().AddPolicy(DeskMcpEndpoints.Policy, policy => policy
            .AddAuthenticationSchemes(DeskMcpAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser());
        var json = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
        };
        builder.Services.AddStatelessMcpServer().WithTools<DeskReadTools>(json);
        return builder;
    }

    /// <summary>Registers JSON errors for the HTTP surface.</summary>
    private static WebApplicationBuilder AddHttpSurface(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpProblemDetails();
        return builder;
    }

    /// <summary>Configures public technical status and SPA endpoints without business mutations.</summary>
    private static WebApplication AddFoundationEndpoints(this WebApplication app)
    {
        app.UseHttpProblemDetails();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseDeskMcpBoundary();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/auth") || context.Request.Path.StartsWithSegments("/api/organizations"))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.Headers.Pragma = "no-cache";
                    return Task.CompletedTask;
                });
                if (!context.Request.IsHttps)
                {
                    await AccessProblems.Create(400, "HTTPS is required", "https_required").ExecuteAsync(context);
                    return;
                }
            }
            await next(context);
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api")
                && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
                && !HttpMethods.IsOptions(context.Request.Method) && !HttpMethods.IsTrace(context.Request.Method)
                && context.GetEndpoint()?.Metadata.GetMetadata<SkipAntiforgeryValidation>() is null)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(context.Request.Headers["X-CSRF-TOKEN"]))
                        throw new AntiforgeryValidationException("Request token is missing.");
                    await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    await AccessProblems.Create(403, "Refresh the page before trying again", "csrf_validation_failed").ExecuteAsync(context);
                    return;
                }
            }
            await next(context);
        });
        app.UseAntiforgery();
        app.MapGet("/api/system/version", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { service = "BrandWorkspace", version = BuildIdentity.Version });
        }).AllowAnonymous();
        app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
        app.MapGet("/health/ready", async (IFoundationRepository repository, CancellationToken token) =>
        {
            try
            {
                return await repository.IsReadyAsync(token) ? Results.Ok(new { status = "ready" })
                    : Results.Problem(statusCode: 503, title: "Database schema is not ready");
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                return Results.Problem(statusCode: 503, title: "Database schema is not ready");
            }
        }).AllowAnonymous();
        app.MapGet("/api/system/status", async (IFoundationRepository repository, TimeProvider time, CancellationToken token) =>
        {
            try
            {
                if (!await repository.IsReadyAsync(token))
                    return Results.Problem(statusCode: 503, title: "Database schema is not ready");
                var worker = WorkerSnapshot.From(await repository.GetLastHeartbeatAsync(token), time.GetUtcNow());
                return Results.Ok(new { service = "brand-workspace", stage = "foundation", persistence = "PostgreSQL", worker });
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                return Results.Problem(statusCode: 503, title: "Database schema is not ready");
            }
        }).AllowAnonymous();
        app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: 404, title: "Not Found"))
            .WithMetadata(new SkipAntiforgeryValidation()).AllowAnonymous();
        app.MapFallbackToFile("index.html").AllowAnonymous();
        return app;
    }

    /// <summary>Runs the explicit migration command or starts the API; neither path starts a worker.</summary>
    public static async Task RunCommandOrHostAsync(this WebApplication app, string[] args)
    {
        if (args.Contains("--verify-media-runtime"))
        {
            if (args.Length != 1) throw new ArgumentException("Use --verify-media-runtime alone.");
            try { MediaRuntimeVerification.Run(app.Services.GetRequiredService<ImageValidationService>()); }
            finally { await app.DisposeAsync(); }
            return;
        }
        if (args.Length == 1 && args[0] == "--migrate")
        {
            var result = await app.Services.GetRequiredService<IMigrationRunnerService>().ApplyPendingAsync("api-cli");
            if (result is not Result<IReadOnlyList<string>>.Success success)
                throw new InvalidOperationException("Migration failed; inspect schema history and source integrity.");
            // Resolving the storage initializes its provider-owned schema only in this explicit CLI.
            _ = app.Services.GetRequiredService<JobStorage>();
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { migrated = success.Value, queueSchema = "ready" }));
            await app.DisposeAsync();
            return;
        }
        if (args.Contains("--migrate")) throw new ArgumentException("Use --migrate alone.");
        if (args.Contains("--import-sourcing"))
        {
            try { await SourcingImportCommand.RunAsync(app.Services, args); }
            finally { await app.DisposeAsync(); }
            return;
        }
        if (args.Contains("--import-events"))
        {
            try { await EventImportCommand.RunAsync(app.Services, args); }
            finally { await app.DisposeAsync(); }
            return;
        }
        if (args.Contains("--provision-operator"))
        {
            try { await OperatorProvisioning.RunAsync(app.Services, args); }
            finally { await app.DisposeAsync(); }
            return;
        }
        await app.RunAsync();
    }
}
