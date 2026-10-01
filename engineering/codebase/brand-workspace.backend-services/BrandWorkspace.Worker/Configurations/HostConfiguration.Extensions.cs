using System.Text.Json;
using BrandWorkspace.Application.Foundation.Repositories;
using BrandWorkspace.Infrastructure.Foundation.BackgroundServices;
using BrandWorkspace.Infrastructure.Foundation.Jobs;
using BrandWorkspace.Persistence.Foundation.Repositories;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire;

namespace BrandWorkspace.Worker.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers the permitted database deployment input.</summary>
    private static HostApplicationBuilder AddExplicitEnvironment(this HostApplicationBuilder builder)
    {
        foreach (var source in builder.Configuration.Sources.OfType<EnvironmentVariablesConfigurationSource>().ToArray())
            builder.Configuration.Sources.Remove(source);
        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
        if (Environment.GetEnvironmentVariable("ConnectionStrings__Database") is { } connection)
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connection });
        return builder;
    }

    /// <summary>Registers the PostgreSQL pool and foundation repository.</summary>
    private static HostApplicationBuilder AddPostgresDatabase(this HostApplicationBuilder builder)
    {
        var connection = builder.Configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection));
        builder.Services.AddSingleton<IFoundationRepository, FoundationRepository>();
        return builder;
    }

    /// <summary>Registers explicit durable execution or a client without hosted job processing.</summary>
    private static HostApplicationBuilder AddFoundation(this HostApplicationBuilder builder, bool executeJobs)
    {
        var connection = builder.Configuration.GetConnectionString("Database")!;
        void ConfigureStorage(IGlobalConfiguration configuration) => configuration.UsePostgreSqlStorage(
            storage => storage.UseNpgsqlConnection(connection),
            new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = false, QueuePollInterval = TimeSpan.FromSeconds(1) });
        if (executeJobs)
        {
            builder.Services.AddHangfireJobs(ConfigureStorage, options =>
            {
                options.WorkerCount = 1;
                options.Queues.Add("foundation");
            });
            builder.Services.AddTransient<DiagnosticProbeJob>();
            builder.Services.AddHostedService<WorkerHeartbeatBackgroundService>();
        }
        else
        {
            // The SDK currently couples client and server. Its underlying public client API starts no server.
            builder.Services.AddHangfire(configuration =>
            {
                configuration.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings();
                ConfigureStorage(configuration);
            });
            builder.Logging.ClearProviders();
        }
        return builder;
    }

    /// <summary>Runs diagnostic CLI operations or starts durable job execution.</summary>
    public static async Task<int> RunCommandOrHostAsync(this IHost host, string[] args)
    {
        var repository = host.Services.GetRequiredService<IFoundationRepository>();
        if (!await repository.IsReadyAsync(CancellationToken.None))
            throw new InvalidOperationException("Database schema is not ready; run the API --migrate command first.");
        if (args.Length == 0) { await host.RunAsync(); return 0; }
        if (args is ["--enqueue-probe"])
        {
            var probeId = Guid.NewGuid();
            var client = host.Services.GetRequiredService<IBackgroundJobClient>();
            var enqueuedJobId = client.Enqueue<DiagnosticProbeJob>(job => job.ExecuteAsync(probeId, CancellationToken.None));
            Console.WriteLine(JsonSerializer.Serialize(new { jobId = enqueuedJobId, probeId }));
            return 0;
        }
        if (args is ["--probe-status", var jobId])
        {
            var storage = host.Services.GetRequiredService<JobStorage>();
            using var connection = storage.GetConnection();
            var data = connection.GetJobData(jobId);
            if (data is null) { Console.WriteLine(JsonSerializer.Serialize(new { jobId, state = "missing" })); return 2; }
            if (data.Job.Type != typeof(DiagnosticProbeJob)) throw new ArgumentException("Job is not a foundation diagnostic probe.");
            var probeId = (Guid)data.Job.Args[0];
            var completedAtUtc = await repository.GetProbeAsync(probeId, CancellationToken.None);
            Console.WriteLine(JsonSerializer.Serialize(new { jobId, probeId, state = data.State, completedAtUtc }));
            return 0;
        }
        Console.Error.WriteLine("Usage: [--enqueue-probe | --probe-status <jobId>]");
        return 2;
    }
}
