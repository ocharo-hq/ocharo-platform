using OcharoCatalogue.Application.Catalogue.Publishing.Commands;

namespace OcharoCatalogue.Api.Catalogue.Publishing.Requests;

/// <summary>Represents the body of a request to withdraw a design's published releases.</summary>
/// <param name="ExpectedRevision">The design revision the caller withdraws.</param>
public sealed record WithdrawReleaseApiRequest(int ExpectedRevision);

/// <summary>Extends <see cref="WithdrawReleaseApiRequest"/> with its mapping to the withdraw command.</summary>
public static class WithdrawReleaseApiRequestExtensions
{
    /// <summary>Maps the request to the command withdrawing the releases of design <paramref name="id"/>.</summary>
    /// <param name="request">The request body.</param>
    /// <param name="id">The design id from the route.</param>
    public static ReleaseWithdrawCommand ToCommand(this WithdrawReleaseApiRequest request, Guid id)
    {
        return new ReleaseWithdrawCommand { DesignId = id, ExpectedRevision = request.ExpectedRevision };
    }
}
