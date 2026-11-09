using System.Text.Json;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Identity;

namespace BrandWorkspace.Api.Events;

/// <summary>Imports an events bundle as an existing editor; never creates operators or memberships.</summary>
public static class EventImportCommand
{
    /// <summary>Holds the word after <c>import</c> that selects this command.</summary>
    public const string Name = "events";

    private const string Usage =
        "Use import events --bundle <bundle.json> --organization <organization-id> --actor <operator-email> [--dry-run].";

    /// <summary>Runs <c>import events</c> over the built host's services: the report goes to <paramref name="output"/>, a refusal to
    /// <paramref name="error"/>.</summary>
    /// <param name="services">The host's root provider.</param>
    /// <param name="args">The arguments after <c>import events</c>.</param>
    /// <param name="output">Standard output.</param>
    /// <param name="error">Standard error.</param>
    /// <param name="cancellationToken">Token to cancel the import.</param>
    /// <returns><c>0</c> imported, <c>1</c> refused, <c>2</c> imported with failures; wrong arguments throw.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        bool dryRun = false;
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == "--dry-run" && !dryRun) { dryRun = true; continue; }
            if (args[index] is not ("--bundle" or "--organization" or "--actor") || index + 1 >= args.Length
                || !options.TryAdd(args[index], args[index + 1]))
                throw new ArgumentException(Usage);
            index++;
        }
        if (options.Count != 3 || !Guid.TryParse(options["--organization"], out Guid organizationId))
            throw new ArgumentException(Usage);

        using IServiceScope scope = services.CreateScope();
        AccessUser user = await scope.ServiceProvider.GetRequiredService<UserManager<AccessUser>>().FindByEmailAsync(options["--actor"])
            ?? throw new InvalidOperationException("The actor is not a provisioned operator.");
        OrganizationAccess access = await scope.ServiceProvider.GetRequiredService<IAccessRepository>()
            .FindMembershipAsync(organizationId, user.Id, cancellationToken)
            ?? throw new InvalidOperationException("The actor is not a member of that organization.");
        try
        {
            EventImportReport report = await scope.ServiceProvider.GetRequiredService<EventImportService>()
                .ImportAsync(access, options["--bundle"], dryRun, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            return report.Failures.Count > 0 ? 2 : 0;
        }
        catch (PreparationException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }
}
