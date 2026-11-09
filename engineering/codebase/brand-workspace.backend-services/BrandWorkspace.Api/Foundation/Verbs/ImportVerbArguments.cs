using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Repositories;

namespace BrandWorkspace.Api.Foundation.Verbs;

/// <summary>
/// Represents an import verb's arguments, its actor resolved to a person Desk knows from Identity, who authors the
/// imported records as an editor of an organization Desk knows; an import never creates people or organizations.
/// </summary>
/// <remarks>
/// Identity holds every membership and role since the cutover, and a verb runs without a sign-in, so Desk checks only
/// that it has seen both the person and the organization; whoever runs the verb on the image already holds Desk's
/// database.
/// </remarks>
/// <param name="Access">The actor's editor access in the organization.</param>
/// <param name="BundlePath">The bundle file's path.</param>
/// <param name="DryRun">Whether the import only reports.</param>
public sealed record ImportVerbArguments(OrganizationAccess Access, string BundlePath, bool DryRun)
{
    /// <summary>Holds the role an import acts in: it edits, and approves nothing.</summary>
    public const string ActorRole = "editor";

    /// <summary>
    /// Parses <c>--bundle &lt;bundle.json&gt; --organization &lt;organization-id&gt; --actor &lt;identity-user-id or
    /// email&gt; [--dry-run]</c> and resolves the actor; wrong arguments throw <paramref name="usage"/>, an actor Desk
    /// never saw sign in or an organization it never saw refuses.
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
        if (options.Count != 3 || !Guid.TryParse(options["--organization"], out var organizationId)
            || string.IsNullOrWhiteSpace(options["--actor"]))
            throw new ArgumentException(usage);
        var repository = services.GetRequiredService<IAccessRepository>();
        var userId = await repository.FindPersonAsync(options["--actor"], cancellationToken)
            ?? throw new InvalidOperationException("The actor is not a provisioned operator.");
        _ = await repository.FindOrganizationAsync(organizationId, ActorRole, cancellationToken)
            ?? throw new InvalidOperationException("Desk knows no such organization.");
        var access = new OrganizationAccess(organizationId, userId, ActorRole, CanEdit: true, CanApprove: false);
        return new ImportVerbArguments(access, options["--bundle"], dryRun);
    }
}
