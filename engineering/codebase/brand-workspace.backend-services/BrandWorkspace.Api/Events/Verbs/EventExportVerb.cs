using System.Text.Json;
using BrandWorkspace.Application.Access.Repositories;
using BrandWorkspace.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace BrandWorkspace.Api.Events.Verbs;

/// <summary>
/// Writes an organization's events as a WoW2 Events import bundle: <c>export events</c>. It reads Desk and writes only
/// the new bundle file (events-adoption.md #6).
/// </summary>
public static class EventExportVerb
{
    /// <summary>Holds the word after <c>export</c> that selects this verb.</summary>
    public const string Name = "events";

    /// <summary>Holds the verb's usage.</summary>
    public const string Usage = "Use export events --organization <organization-id> --out <bundle.json>.";

    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Runs <c>export events</c> over the built host's services.</summary>
    /// <param name="services">The host's root provider.</param>
    /// <param name="args">The arguments after <c>export events</c>.</param>
    /// <param name="output">Standard output, which receives the report.</param>
    /// <param name="error">Standard error, which receives a refusal.</param>
    /// <param name="cancellationToken">The token that cancels the export.</param>
    /// <returns><c>0</c> exported, <c>1</c> refused; wrong arguments throw.</returns>
    public static async Task<int> RunAsync(
        IServiceProvider services,
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args is not ["--organization", var organization, "--out", var bundle]
            || !Guid.TryParse(organization, out var organizationId) || string.IsNullOrWhiteSpace(bundle))
            throw new ArgumentException(Usage);
        using var scope = services.CreateScope();
        if (await scope.ServiceProvider.GetRequiredService<IAccessRepository>()
                .FindOrganizationAsync(organizationId, "viewer", cancellationToken) is null)
        {
            await error.WriteLineAsync("Desk knows no such organization.");
            return 1;
        }
        try
        {
            var report = await scope.ServiceProvider.GetRequiredService<EventExportService>()
                .ExportAsync(organizationId, Path.GetFullPath(bundle), cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(report, ReportJson));
            return 0;
        }
        catch (AppException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }
}
