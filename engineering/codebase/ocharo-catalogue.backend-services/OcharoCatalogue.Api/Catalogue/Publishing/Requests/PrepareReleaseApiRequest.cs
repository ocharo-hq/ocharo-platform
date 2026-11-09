using OcharoCatalogue.Application.Catalogue.Publishing.Commands;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;

namespace OcharoCatalogue.Api.Catalogue.Publishing.Requests;

/// <summary>Represents the JSON body of a request to prepare a release without rendered artifacts.</summary>
/// <param name="ExpectedRevision">The design revision the caller rendered.</param>
/// <param name="Fingerprint">The fingerprint the preparation details gave.</param>
public sealed record PrepareReleaseApiRequest(int ExpectedRevision, string? Fingerprint);

/// <summary>Extends <see cref="PrepareReleaseApiRequest"/> with its mapping to the prepare command.</summary>
public static class PrepareReleaseApiRequestExtensions
{
    /// <summary>Maps the request to the command preparing a release of design <paramref name="id"/>.</summary>
    /// <param name="request">The request body.</param>
    /// <param name="id">The design id from the route.</param>
    public static ReleasePrepareCommand ToCommand(this PrepareReleaseApiRequest request, Guid id)
    {
        return new ReleasePrepareCommand
        {
            DesignId = id,
            Metadata = new ReleasePrepareMetadata(request.ExpectedRevision, request.Fingerprint),
        };
    }
}
