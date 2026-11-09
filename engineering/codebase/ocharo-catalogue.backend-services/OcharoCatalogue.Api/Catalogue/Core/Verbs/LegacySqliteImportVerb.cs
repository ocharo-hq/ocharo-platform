using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using OcharoCatalogue.Persistence.Import;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace OcharoCatalogue.Api.Catalogue.Core.Verbs;

/// <summary>Answers <c>import-sqlite [--dry-run]</c>: copies the retired SQLite store and key ring into the product
/// database once, verifies every row, then retires the SQLite file.</summary>
/// <remarks>
///   - run after the deploy's <c>migrate</c> step, on the target's image with the <c>catalogue-data</c> volume mounted, as
///     an operator job
///   - exits <c>0</c> copied, planned or nothing to copy, <c>1</c> refused (a diverged row, a broken key, an unknown
///     argument, no schema yet), <c>2</c> failed; the last stdout line is <c>import-result {json}</c> with counts and
///     digests only
/// </remarks>
public static class LegacySqliteImportVerb
{
    public const string Name = "import-sqlite";

    private static readonly JsonSerializerOptions ResultJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Task<int> RunAsync(IServiceProvider services, ServiceVerbInvocation invocation, CancellationToken ct) =>
        RunAsync(services, invocation.Arguments, invocation.Output, invocation.Error, ct);

    /// <summary>Runs the verb over a host's services with the given streams: the seam its E2E tier calls.</summary>
    public static async Task<int> RunAsync(IServiceProvider services, string[] arguments, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (arguments.Any(argument => argument != "--dry-run"))
        {
            await error.WriteLineAsync($"Usage: {Name} [--dry-run]");
            return Write(output, LegacySqliteImportStatus.Refused, "Unknown argument.", 1);
        }

        try
        {
            await using var scope = services.CreateAsyncScope();
            var report = await scope.ServiceProvider.GetRequiredService<LegacySqliteImport>()
                .RunAsync(arguments.Contains("--dry-run"), ct);
            await error.WriteLineAsync(report.Summary);
            await output.WriteLineAsync($"import-result {JsonSerializer.Serialize(report, ResultJson)}");
            return report.Status == LegacySqliteImportStatus.Refused ? 1 : 0;
        }
        catch (PostgresException failure) when (failure.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.InvalidSchemaName)
        {
            await error.WriteLineAsync("The catalogue schema is missing: run `migrate` first.");
            return Write(output, LegacySqliteImportStatus.Refused, "The catalogue schema is missing.", 1);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // A connection failure's message names the server; a server error's names only the statement's fault.
            await error.WriteLineAsync(failure is NpgsqlException and not PostgresException
                ? "The import failed: the product database could not be reached."
                : $"The import failed: {failure.GetType().Name}: {failure.Message}");
            await output.WriteLineAsync("import-result {\"status\":\"failed\"}");
            return 2;
        }
    }

    private static int Write(TextWriter output, LegacySqliteImportStatus status, string summary, int exit)
    {
        var report = new LegacySqliteImportReport { Status = status, Summary = summary };
        output.WriteLine($"import-result {JsonSerializer.Serialize(report, ResultJson)}");
        return exit;
    }
}
