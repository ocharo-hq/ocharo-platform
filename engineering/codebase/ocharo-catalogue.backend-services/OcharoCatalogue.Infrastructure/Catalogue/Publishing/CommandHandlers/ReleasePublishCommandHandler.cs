using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Publishing.Commands;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.CommandHandlers;

/// <summary>Handles <see cref="ReleasePublishCommand"/>.</summary>
public sealed class ReleasePublishCommandHandler(
    IDesignRepository designs,
    MediaVerificationService media,
    TimeProvider timeProvider) : ICommandHandler<ReleasePublishCommand, AppResult<CatalogueReleaseSummary>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CatalogueReleaseSummary>> HandleAsync(
        ReleasePublishCommand request,
        CancellationToken ct)
    {
        var record = await designs.ReleaseAsync(request.ReleaseId, ct);
        if (record is null || record.DesignId != request.DesignId)
        {
            return AppResult<CatalogueReleaseSummary>.Fail(CatalogueErrors.NotFound("Prepared release"));
        }

        var images = CatalogueJson.Read<CataloguePreparedRelease>(record.Json).Products
            .SelectMany(product => product.Images)
            .Select(image => image.Sha256);
        if (await media.VerifyAsync(images, ct) is { } error)
        {
            return AppResult<CatalogueReleaseSummary>.Fail(error);
        }

        var published = await designs.PublishAsync(
            request.DesignId,
            request.ReleaseId,
            request.ExpectedRevision,
            timeProvider.GetUtcNow(),
            ct);
        return AppResult<CatalogueReleaseSummary>.Ok(ReleaseSummaries.ToSummary(published));
    }
}
