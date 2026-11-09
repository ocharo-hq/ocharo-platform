using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Api.Foundation.Verbs;

/// <summary>
/// Writes an import verb's outcome: the report as indented JSON on standard output, a refusal on standard error.
/// </summary>
public static class ImportVerbOutcome
{
    /// <summary>Holds the report's JSON form.</summary>
    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Writes <paramref name="result"/> and returns the verb's exit code: <c>0</c> imported, <c>1</c> refused, <c>2</c>
    /// imported with failures; an unexpected failure throws, so it leaves through the CLI's exception path.
    /// </summary>
    /// <typeparam name="TReport">The report's type.</typeparam>
    /// <param name="result">The import's result.</param>
    /// <param name="hasFailures">Tells whether a report lists failed items.</param>
    /// <param name="output">Standard output.</param>
    /// <param name="error">Standard error.</param>
    public static async Task<int> WriteAsync<TReport>(AppResult<TReport> result, Func<TReport, bool> hasFailures,
        TextWriter output, TextWriter error)
        where TReport : notnull
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(hasFailures);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        return await result.Match<Task<int>>(
            async ok =>
            {
                await output.WriteLineAsync(JsonSerializer.Serialize(ok.Data, ReportJson));
                return hasFailures(ok.Data) ? 2 : 0;
            },
            async fail =>
            {
                if (fail.Error.Is(AppErrorType.Unexpected) || fail.Error.Is(AppErrorType.Canceled))
                    throw fail.Error.ToException();
                await error.WriteLineAsync(fail.Error.Message);
                return 1;
            });
    }
}
