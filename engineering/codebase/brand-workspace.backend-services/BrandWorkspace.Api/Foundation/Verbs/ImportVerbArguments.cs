using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Repositories;
using BrandWorkspace.Persistence.Access.Models;
using Microsoft.AspNetCore.Identity;

namespace BrandWorkspace.Api.Foundation.Verbs;

/// <summary>
/// Represents an import verb's arguments, its actor resolved to an existing editor's membership; an import never
/// creates operators or memberships.
/// </summary>
/// <param name="Access">The actor's membership in the organization.</param>
/// <param name="BundlePath">The bundle file's path.</param>
/// <param name="DryRun">Whether the import only reports.</param>
public sealed record ImportVerbArguments(OrganizationAccess Access, string BundlePath, bool DryRun)
{
    /// <summary>
    /// Parses <c>--bundle &lt;bundle.json&gt; --organization &lt;organization-id&gt; --actor &lt;operator-email&gt;
    /// [--dry-run]</c> and resolves the actor; wrong arguments throw <paramref name="usage"/>, an unknown actor or a
    /// non-member refuses.
    /// </summary>
    /// <param name="services">A scope's provider.</param>
    /// <param name="args">The arguments after the verb's name.</param>
    /// <param name="usage">The usage the verb reports on wrong arguments.</param>
    /// <param name="cancellationToken">The token that cancels the lookup.</param>
    public static async Task<ImportVerbArguments> ResolveAsync(IServiceProvider services, string[] args, string usage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var dryRun = false;
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == "--dry-run" && !dryRun)
            {
                dryRun = true;
                continue;
            }
            if (args[index] is not ("--bundle" or "--organization" or "--actor") || index + 1 >= args.Length
                || !options.TryAdd(args[index], args[index + 1]))
                throw new ArgumentException(usage);
            index++;
        }
        if (options.Count != 3 || !Guid.TryParse(options["--organization"], out var organizationId))
            throw new ArgumentException(usage);
        var user = await services.GetRequiredService<UserManager<AccessUser>>().FindByEmailAsync(options["--actor"])
            ?? throw new InvalidOperationException("The actor is not a provisioned operator.");
        var access = await services
            .GetRequiredService<IAccessRepository>()
            .FindMembershipAsync(organizationId, user.Id, cancellationToken)
            ?? throw new InvalidOperationException("The actor is not a member of that organization.");
        return new ImportVerbArguments(access, options["--bundle"], dryRun);
    }
}
