using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Services;
using OcharoCatalogue.Application.Catalogue.Media.Models;
using OcharoCatalogue.Application.Catalogue.Media.Queries;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Infrastructure.Catalogue.Media.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Media.QueryHandlers;

/// <summary>Handles <see cref="MediaGetQuery"/>.</summary>
public sealed class MediaGetQueryHandler(
    MediaAccessService access,
    IMediaStore media,
    ICatalogueCaller caller)
    : IQueryHandler<MediaGetQuery, AppResult<MediaDelivery>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<MediaDelivery>> HandleAsync(MediaGetQuery request, CancellationToken ct)
    {
        var isPublic = await access.IsPublicAsync(request.Sha256, ct);

        // Private bytes answer as missing outside the bound organization, so a caller cannot probe for them.
        if (!isPublic && !caller.IsMember)
        {
            return AppResult<MediaDelivery>.Fail(CatalogueErrors.NotFound("Media"));
        }

        var opened = await media.OpenAsync(
            request.Sha256,
            request.Preview,
            ct);
        if (opened is null)
        {
            return AppResult<MediaDelivery>.Fail(CatalogueErrors.NotFound("Media"));
        }

        return AppResult<MediaDelivery>.Ok(new MediaDelivery(isPublic, opened));
    }
}
