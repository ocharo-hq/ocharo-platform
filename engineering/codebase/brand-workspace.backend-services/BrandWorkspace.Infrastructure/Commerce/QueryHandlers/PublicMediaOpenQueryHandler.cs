using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.QueryHandlers;

/// <summary>Handles <see cref="PublicMediaOpenQuery"/>.</summary>
public sealed class PublicMediaOpenQueryHandler(ICommerceRepository repository, CommerceMediaFiles files)
    : IQueryHandler<PublicMediaOpenQuery, AppResult<StoredContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<StoredContent>> HandleAsync(PublicMediaOpenQuery request, CancellationToken cancellationToken)
    {
        if (await repository.PublicAsync(request.Slug, request.Release, cancellationToken) is not { } release
            || !CommerceRules.ApprovedHashes(release.Snapshot).Contains(request.Sha256)
            || await files.OpenAsync(release.BrandId, request.Sha256, false, cancellationToken) is not { } media)
            return AppResult<StoredContent>.Fail(DeskErrors.NotFound("The media was not found."));
        return AppResult<StoredContent>.Ok(media);
    }
}
