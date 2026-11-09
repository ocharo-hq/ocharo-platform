using OcharoCatalogue.Application.Catalogue.Publishing.Commands;

namespace OcharoCatalogue.Api.Catalogue.Publishing.Requests;

/// <summary>Represents the body of a request to publish a prepared release.</summary>
/// <param name="ExpectedRevision">The design revision the caller publishes.</param>
public sealed record PublishReleaseApiRequest(int ExpectedRevision);

/// <summary>Extends <see cref="PublishReleaseApiRequest"/> with its mapping to the publish command.</summary>
public static class PublishReleaseApiRequestExtensions
{
    /// <summary>Maps the request to the command publishing release <paramref name="releaseId"/> of design
    /// <paramref name="id"/>.</summary>
    /// <param name="request">The request body.</param>
    /// <param name="id">The design id from the route.</param>
    /// <param name="releaseId">The release id from the route.</param>
    public static ReleasePublishCommand ToCommand(
        this PublishReleaseApiRequest request,
        Guid id,
        Guid releaseId)
    {
        return new ReleasePublishCommand
        {
            DesignId = id,
            ReleaseId = releaseId,
            ExpectedRevision = request.ExpectedRevision,
        };
    }
}
