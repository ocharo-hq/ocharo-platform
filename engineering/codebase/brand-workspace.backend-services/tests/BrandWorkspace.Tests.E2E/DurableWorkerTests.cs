using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api.Configurations;
using BrandWorkspace.Application.Foundation.Repositories;
using BrandWorkspace.Infrastructure.Foundation.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;
using Xunit;
using WorkerConfiguration = BrandWorkspace.Worker.Configurations.HostConfiguration;

namespace BrandWorkspace.Tests.E2E;

public sealed class DurableWorkerTests
{
    [Fact]
    public async Task Worker_ShouldCompleteDurableProbe_WhenDatabaseAndWorkerRestart()
    {
        var container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
        await using var database = new PostgresFixture(container);
        await database.StartAsync();
        var connection = database.ConnectionString;

        // An API pointed at an empty database remains live but cannot change the schema.
        await using (var unmigrated = ApiContractTests.CreateFactory(connection))
        {
            using var client = unmigrated.CreateClient();
            (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }
        await using (var dataSource = NpgsqlDataSource.Create(connection))
        await using (var command = dataSource.CreateCommand("SELECT to_regnamespace('foundation') IS NULL"))
            (await command.ExecuteScalarAsync()).Should().Be(true);

        using (var unmigratedWorker = CreateWorker(connection, executeJobs: false))
        {
            var startup = () => WorkerConfiguration.ConfigureAsync(unmigratedWorker);
            var failure = await startup.Should().ThrowAsync<PostgresException>();
            failure.Which.SqlState.Should().Be(PostgresErrorCodes.UndefinedTable);
        }

        await MigrateAsync(connection);
        await MigrateAsync(connection); // Applying the same embedded source is idempotent.
        string jobId;
        var probeId = Guid.NewGuid();
        using (var clientHost = CreateWorker(connection, executeJobs: false))
        {
            clientHost.Services.GetServices<IHostedService>().Should().BeEmpty();
            jobId = clientHost.Services.GetRequiredService<IBackgroundJobClient>()
                .Enqueue<DiagnosticProbeJob>(job => job.ExecuteAsync(probeId, CancellationToken.None));
        }
        await using (var api = ApiContractTests.CreateFactory(connection))
        {
            using var client = api.CreateClient();
            new NpgsqlConnectionStringBuilder(api.Services.GetRequiredService<NpgsqlDataSource>().ConnectionString)
                .Port.Should().Be(new NpgsqlConnectionStringBuilder(connection).Port);
            (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);
            var status = await client.GetFromJsonAsync<JsonElement>("/api/system/status");
            status.GetProperty("service").GetString().Should().Be("brand-workspace");
            status.GetProperty("worker").GetProperty("state").GetString().Should().Be("waiting");
            api.Services.GetService<IBackgroundJobClient>().Should().BeNull();
            (await api.Services.GetRequiredService<IFoundationRepository>().GetProbeAsync(probeId, CancellationToken.None)).Should().BeNull();
        }

        // Restart the same PostgreSQL container, preserving its persisted database files.
        await container.StopAsync();
        await container.StartAsync();
        // Docker may allocate a different ephemeral host port when restarting the same container.
        connection = database.ConnectionString;
        using (var worker = CreateWorker(connection, executeJobs: true))
        {
            await WorkerConfiguration.ConfigureAsync(worker);
            await worker.StartAsync();
            try
            {
                await AssertCompletedAsync(worker, jobId, probeId);
                (await worker.Services.GetRequiredService<IFoundationRepository>().GetLastHeartbeatAsync(CancellationToken.None)).Should().NotBeNull();
            }
            finally { await worker.StopAsync(); }
        }

        var secondProbe = Guid.NewGuid();
        string secondJob;
        using (var clientHost = CreateWorker(connection, executeJobs: false))
            secondJob = clientHost.Services.GetRequiredService<IBackgroundJobClient>()
                .Enqueue<DiagnosticProbeJob>(job => job.ExecuteAsync(secondProbe, CancellationToken.None));
        using (var restartedWorker = CreateWorker(connection, executeJobs: true))
        {
            await WorkerConfiguration.ConfigureAsync(restartedWorker);
            await restartedWorker.StartAsync();
            try
            {
                await AssertCompletedAsync(restartedWorker, secondJob, secondProbe);
                var repository = restartedWorker.Services.GetRequiredService<IFoundationRepository>();
                var originalReceipt = await repository.GetProbeAsync(probeId, CancellationToken.None);
                await repository.RecordProbeAsync(probeId, CancellationToken.None);
                (await repository.GetProbeAsync(probeId, CancellationToken.None)).Should().Be(originalReceipt);
            }
            finally { await restartedWorker.StopAsync(); }
        }
    }

    private static async Task MigrateAsync(string connection)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connection });
        builder.Configure(migrate: true);
        var app = builder.Build();
        app.Configure();
        await app.RunCommandOrHostAsync(["--migrate"]);
    }

    private static IHost CreateWorker(string connection, bool executeJobs)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connection });
        WorkerConfiguration.Configure(builder, executeJobs);
        return builder.Build();
    }

    private static async Task AssertCompletedAsync(IHost worker, string jobId, Guid probeId)
    {
        var repository = worker.Services.GetRequiredService<IFoundationRepository>();
        var receipt = await PollingExtensions.UntilAsync(
            () => repository.GetProbeAsync(probeId, CancellationToken.None),
            timestamp => timestamp is not null, timeout: TimeSpan.FromSeconds(30));
        receipt.Should().NotBeNull();
        var storage = worker.Services.GetRequiredService<JobStorage>();
        var state = await PollingExtensions.UntilAsync(() =>
        {
            using var connection = storage.GetConnection();
            return Task.FromResult(connection.GetJobData(jobId)?.State);
        }, value => value == "Succeeded", timeout: TimeSpan.FromSeconds(10));
        state.Should().Be("Succeeded");
    }
}
