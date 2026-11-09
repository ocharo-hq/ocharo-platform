using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using OcharoBrand.Persistence.Import;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace OcharoBrand.Api.Brand.Import;

/// <summary>Answers <c>import-sqlite [--dry-run]</c>: copies the retired SQLite store and key ring into the product
/// database once, verifies every table, then retires the SQLite file.</summary>
/// <remarks>
///   - run after the deploy's <c>migrate</c> step, on the target's image with the <c>brand-data</c> volume mounted, as an
///     operator job
///   - exits <c>0</c> copied, planned or nothing to copy, <c>1</c> refused (a diverged row, a missing key, a blob whose
///     bytes disagree with its SHA-256, an unknown argument, no schema yet), <c>2</c> failed; the last stdout line is
///     <c>import-result {json}</c> with counts and digests only
/// </remarks>
public static class LegacySqliteImportVerb
{
    public const string Name = "import-sqlite";

    private const string DryRun = "--dry-run";

    private static readonly JsonSerializerOptions ResultJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static Task<int> RunAsync(IServiceProvider services, ServiceVerbInvocation invocation, CancellationToken ct) =>
        RunAsync(services, invocation.Arguments, invocation.Output, invocation.Error, ct);

    /// <summary>Runs the verb over a host's services with the given streams: the seam its E2E tier calls.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, IReadOnlyList<string> arguments, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (arguments.Any(argument => argument != DryRun))
        {
            await error.WriteLineAsync($"Usage: {Name} [{DryRun}]");
            return await WriteAsync(output, LegacySqliteImportStatus.Refused, "Unknown argument.", 1);
        }

        try
        {
            var report = await services.GetRequiredService<LegacySqliteImport>().RunAsync(arguments.Contains(DryRun), ct);
            await error.WriteLineAsync(report.Summary);
            await output.WriteLineAsync($"import-result {JsonSerializer.Serialize(report, ResultJson)}");
            return report.Status == LegacySqliteImportStatus.Refused ? 1 : 0;
        }
        catch (PostgresException failure) when (failure.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        {
            await error.WriteLineAsync("The brand schema is missing: run `migrate` first.");
            return await WriteAsync(output, LegacySqliteImportStatus.Refused, "The brand schema is missing.", 1);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            await error.WriteLineAsync($"The import failed: {failure.GetType().Name}: {failure.Message}");
            await output.WriteLineAsync("import-result {\"status\":\"failed\"}");
            return 2;
        }
    }

    private static async Task<int> WriteAsync(TextWriter output, LegacySqliteImportStatus status, string summary, int exit)
    {
        var report = new LegacySqliteImportReport { Status = status, Summary = summary };
        await output.WriteLineAsync($"import-result {JsonSerializer.Serialize(report, ResultJson)}");
        return exit;
    }
}
