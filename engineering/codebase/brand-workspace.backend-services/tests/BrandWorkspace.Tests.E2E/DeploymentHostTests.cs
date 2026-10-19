using System.Net;
using AwesomeAssertions;
using BrandWorkspace.Api;
using BrandWorkspace.Api.Configurations;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Desk as a deployed target runs it: Production, target settings only, behind the ingress, migrating itself.</summary>
public sealed class DeploymentHostTests
{
    private const string Ingress = "172.30.0.2";

    [Fact]
    public async Task Production_ShouldMigrateAnEmptyDatabaseBeforeItReportsReady()
    {
        await using var database = new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build());
        await database.StartAsync();
        var keys = Directory.CreateTempSubdirectory("brand-workspace-deployed-keys-");
        try
        {
            await using var factory = DeployedFactory(database.ConnectionString, keys.FullName);
            using var client = factory.CreateClient();

            (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/api/system/status")).StatusCode.Should().Be(HttpStatusCode.OK);
            factory.Services.GetService<IBackgroundJobClient>().Should().BeNull("the serving API installs the queue schema without a job client");
            await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString);
            await using var command = dataSource.CreateCommand("SELECT count(*) FROM public.migration_history WHERE applied_by = 'startup'");
            ((long)(await command.ExecuteScalarAsync())!).Should().BePositive();
        }
        finally { keys.Delete(true); }
    }

    [Fact]
    public async Task Startup_ShouldFail_WhenRecordedSchemaHistoryNoLongerMatchesTheSource()
    {
        await using var database = new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build());
        await database.StartAsync();
        await using (var migrated = ApiContractTests.CreateFactory(database.ConnectionString))
            migrated.CreateClient().Dispose();
        await using (var dataSource = NpgsqlDataSource.Create(database.ConnectionString))
        await using (var drift = dataSource.CreateCommand("UPDATE public.migration_history SET checksum = repeat('0', 64) WHERE ordinal = 1"))
            (await drift.ExecuteNonQueryAsync()).Should().Be(1);

        await using var drifted = ApiContractTests.CreateFactory(database.ConnectionString);
        var start = () => drifted.CreateClient();

        start.Should().Throw<Exception>().Where(failure => failure.ToString().Contains("Migration failed"),
            "a host that cannot migrate never starts, so the rollout stops before readiness");
    }

    [Theory]
    [InlineData(Ingress, Ingress, true)]
    [InlineData(Ingress, "203.0.113.9", false)]
    [InlineData(null, Ingress, false)]
    public async Task ForwardedHeaders_ShouldApplyOnlyFromTheConfiguredIngress(string? trustedProxy, string sender, bool trusted)
    {
        var keys = Directory.CreateTempSubdirectory("brand-workspace-proxy-keys-");
        try
        {
            await using var app = await StartBehindIngressAsync(trustedProxy, keys.FullName);
            var context = await app.GetTestServer().SendAsync(request =>
            {
                request.Request.Method = HttpMethods.Get;
                request.Request.Path = "/api/organizations";
                request.Request.Headers["X-Forwarded-For"] = "198.51.100.7";
                request.Request.Headers["X-Forwarded-Proto"] = "https";
                request.Request.Headers["X-Forwarded-Host"] = "desk.example.test";
                request.Connection.RemoteIpAddress = IPAddress.Parse(sender);
            });

            if (trusted)
            {
                context.Request.Scheme.Should().Be("https");
                context.Request.Host.Value.Should().Be("desk.example.test");
                context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse("198.51.100.7"));
                context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized, "sign-in sees HTTPS and only asks for a session");
            }
            else
            {
                context.Request.Scheme.Should().Be("http");
                context.Request.Host.Value.Should().Be("localhost");
                context.Connection.RemoteIpAddress.Should().Be(IPAddress.Parse(sender));
                context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest, "an untrusted sender cannot claim HTTPS");
            }
        }
        finally { keys.Delete(true); }
    }

    /// <summary>A Production host with only the settings a target supplies; no launch profile, no local run.</summary>
    private static WebApiTestHost<ApiAssemblyMarker> DeployedFactory(string connection, string keys)
    {
        var factory = new WebApiTestHost<ApiAssemblyMarker>
        {
            ConfigureHostHook = host => host.UseEnvironment(Environments.Production)
                .ConfigureHostConfiguration(settings => settings.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DEPLOY_ENVIRONMENT"] = "prod", ["Deployment:TrustedProxies:0"] = Ingress
                })),
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = connection,
                ["AllowedHosts"] = "desk.example.test;localhost",
                ["DataProtection:KeysPath"] = keys,
                ["DataProtection:ApplicationName"] = "brand-workspace:deployed-tests"
            })
        };
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        return factory;
    }

    /// <summary>Starts the HTTP pipeline only; these checks reach no database.</summary>
    private static async Task<WebApplication> StartBehindIngressAsync(string? trustedProxy, string keys)
    {
        string[] args =
        [
            "--DEPLOY_ENVIRONMENT=prod", "--AllowedHosts=desk.example.test;localhost",
            "--ConnectionStrings:Database=Host=127.0.0.1;Port=1;Database=unavailable;Username=unused;Timeout=1",
            $"--DataProtection:KeysPath={keys}", "--DataProtection:ApplicationName=brand-workspace:proxy-tests",
            .. trustedProxy is null ? Array.Empty<string>() : [$"--Deployment:TrustedProxies:0={trustedProxy}"]
        ];
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args, EnvironmentName = Environments.Production, ContentRootPath = AppContext.BaseDirectory
        });
        builder.WebHost.UseTestServer();
        builder.Configure();
        var app = builder.Build();
        app.Configure();
        await app.StartAsync();
        return app;
    }
}
