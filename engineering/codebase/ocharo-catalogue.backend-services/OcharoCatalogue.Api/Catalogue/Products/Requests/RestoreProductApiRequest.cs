using OcharoCatalogue.Application.Catalogue.Products.Commands;

namespace OcharoCatalogue.Api.Catalogue.Products.Requests;

/// <summary>Represents the body of a request to restore an older product revision.</summary>
/// <param name="ExpectedRevision">The revision the caller edited.</param>
public sealed record RestoreProductApiRequest(int ExpectedRevision);

/// <summary>Extends <see cref="RestoreProductApiRequest"/> with its mapping to the restore command.</summary>
public static class RestoreProductApiRequestExtensions
{
    /// <summary>
    /// Maps the request to the command restoring <paramref name="revision"/> of product <paramref name="id"/>.
    /// </summary>
    /// <param name="request">The request body.</param>
    /// <param name="id">The product id from the route.</param>
    /// <param name="revision">The revision to restore, from the route.</param>
    public static ProductRestoreCommand ToCommand(
        this RestoreProductApiRequest request,
        string id,
        int revision)
    {
        return new ProductRestoreCommand
        {
            Id = id,
            Revision = revision,
            ExpectedRevision = request.ExpectedRevision,
        };
    }
}
