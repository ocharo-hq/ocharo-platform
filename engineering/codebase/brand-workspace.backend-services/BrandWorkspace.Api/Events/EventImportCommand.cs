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
    private const string Usage =
        "Use --import-events --bundle <bundle.json> --organization <organization-id> --actor <operator-email> [--dry-run].";

    public static async Task RunAsync(IServiceProvider services, string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        bool dryRun = false;
        for (int index = 1; index < args.Length; index++)
        {
            if (args[index] == "--dry-run" && !dryRun) { dryRun = true; continue; }
            if (args[index] is not ("--bundle" or "--organization" or "--actor") || index + 1 >= args.Length
                || !options.TryAdd(args[index], args[index + 1]))
                throw new ArgumentException(Usage);
            index++;
        }
        if (args[0] != "--import-events" || options.Count != 3 || !Guid.TryParse(options["--organization"], out Guid organizationId))
            throw new ArgumentException(Usage);

        using IServiceScope scope = services.CreateScope();
        AccessUser user = await scope.ServiceProvider.GetRequiredService<UserManager<AccessUser>>().FindByEmailAsync(options["--actor"])
            ?? throw new InvalidOperationException("The actor is not a provisioned operator.");
        OrganizationAccess access = await scope.ServiceProvider.GetRequiredService<IAccessRepository>()
            .FindMembershipAsync(organizationId, user.Id, CancellationToken.None)
            ?? throw new InvalidOperationException("The actor is not a member of that organization.");
        try
        {
            EventImportReport report = await scope.ServiceProvider.GetRequiredService<EventImportService>()
                .ImportAsync(access, options["--bundle"], dryRun, CancellationToken.None);
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            if (report.Failures.Count > 0) Environment.ExitCode = 2;
        }
        catch (PreparationException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            Environment.ExitCode = 1;
        }
    }
}
