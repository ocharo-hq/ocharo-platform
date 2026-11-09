using BrandWorkspace.Api.Foundation.Verbs;
using BrandWorkspace.Application.Sourcing.Commands;
using WoW.Two.Sdk.Beta.Be.Mediator;

namespace BrandWorkspace.Api.Sourcing.Verbs;

/// <summary>Imports a private sourcing bundle as an existing editor: <c>import sourcing</c>.</summary>
public static class SourcingImportVerb
{
    /// <summary>Holds the word after <c>import</c> that selects this verb.</summary>
    public const string Name = "sourcing";

    /// <summary>Holds the verb's usage.</summary>
    private const string Usage =
        "Use import sourcing --bundle <bundle.json> --organization <organization-id> --actor <operator-email> [--dry-run].";

    /// <summary>Runs <c>import sourcing</c> over the built host's services.</summary>
    /// <param name="services">The host's root provider.</param>
    /// <param name="args">The arguments after <c>import sourcing</c>.</param>
    /// <param name="output">Standard output, which receives the report.</param>
    /// <param name="error">Standard error, which receives a refusal.</param>
    /// <param name="cancellationToken">The token that cancels the import.</param>
    /// <returns><c>0</c> imported, <c>1</c> refused, <c>2</c> imported with failures; wrong arguments throw.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args, TextWriter output, TextWriter error,
        CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var import = await ImportVerbArguments.ResolveAsync(scope.ServiceProvider, args, Usage, cancellationToken);
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(
            new SourcingImportCommand { Access = import.Access, BundlePath = import.BundlePath, DryRun = import.DryRun }, cancellationToken);
        return await ImportVerbOutcome.WriteAsync(result, report => report.Failures.Count > 0, output, error);
    }
}
