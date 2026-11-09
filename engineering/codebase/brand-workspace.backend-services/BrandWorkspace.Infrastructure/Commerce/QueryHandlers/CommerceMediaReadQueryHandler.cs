using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.QueryHandlers;

/// <summary>Handles <see cref="CommerceMediaReadQuery"/>.</summary>
public sealed class CommerceMediaReadQueryHandler(ICommerceRepository repository, CommerceMediaFiles files)
    : IQueryHandler<CommerceMediaReadQuery, AppResult<StoredContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<StoredContent>> HandleAsync(CommerceMediaReadQuery request, CancellationToken cancellationToken)
    {
        if (!CommerceRules.Hash(request.Sha256))
            return AppResult<StoredContent>.Fail(DeskErrors.NotFound("The media was not found."));
        // Resolve the brand through the organization before reading its private content-addressed bytes.
        if (await repository.GetAsync(request.Access.OrganizationId, request.BrandId, cancellationToken) is null
            || await files.ReadAsync(request.BrandId, request.Sha256, cancellationToken) is not { } media)
            return AppResult<StoredContent>.Fail(DeskErrors.NotFound("The media was not found."));
        return AppResult<StoredContent>.Ok(media);
    }
}
