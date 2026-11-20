using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Tests.Harness;

/// <summary>
/// Stands in for WoW2 Files behind the event image route: each signed-URL request is recorded with the file and the spec
/// the route sent, and answers a link on a fake content host, so a test proves the route asks for the file the event named,
/// never one the browser sent, and redirects to what Files answered. No call reaches a Files instance.
/// </summary>
internal sealed class FakeFileUrlsBroker : IFileUrlsBroker
{
    /// <summary>The content host the fake's links point at.</summary>
    public const string ContentHost = "https://files.example.test/f/";

    /// <summary>Gets every signed-URL request, in order.</summary>
    public List<(FileId Id, UrlSpec Spec)> Issued { get; } = [];

    /// <summary>Gets or sets the failure every request answers instead of a link; none by default.</summary>
    public AppError? Failure { get; set; }

    /// <summary>Gets or sets the exception every request throws, as an unreachable or timing-out Files does; none by default.</summary>
    public Exception? Throws { get; set; }

    /// <summary>Gets the link the fake answers for a file.</summary>
    public static Uri LinkFor(FileId id) => new($"{ContentHost}{id}?exp=1&sig=test");

    public Task<Result<SignedFileUrl>> IssueAsync(FileId id, UrlSpec spec, CancellationToken cancellationToken = default)
    {
        Issued.Add((id, spec));
        if (Throws is { } exception) throw exception;
        return Task.FromResult(Failure is { } failure
            ? Result<SignedFileUrl>.Fail(failure)
            : Result<SignedFileUrl>.Ok(new SignedFileUrl(LinkFor(id), DateTimeOffset.UtcNow.Add(spec.Lifetime))));
    }

    /// <summary>The route asks for signed links only; a public URL here is a mistake.</summary>
    public Task<Result<Uri>> GetPublicUrlAsync(FileId id, int? width = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The event image route never asks for a public URL.");
}
