using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Policies;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.QueryHandlers;

/// <summary>Handles <see cref="ReleasePreparationGetQuery"/>.</summary>
public sealed class ReleasePreparationGetQueryHandler(ReleasePreparationService preparations)
    : IQueryHandler<ReleasePreparationGetQuery, AppResult<ReleasePreparation>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ReleasePreparation>> HandleAsync(
        ReleasePreparationGetQuery request,
        CancellationToken ct)
    {
        var read = await preparations.ReadAsync(
            request.DesignId,
            null,
            ct);
        return read.Match(
            success =>
            {
                var preparation = success.Data;
                var pageIds = ReleaseSelectionPolicy.PdfPages(preparation)
                    .Select(page => page.GetProperty("id").GetString()!)
                    .ToArray();
                var excluded = preparation.References
                    .Where(id => !ReleaseSelectionPolicy.Eligible(preparation, id))
                    .ToArray();
                return AppResult<ReleasePreparation>.Ok(new ReleasePreparation(
                    preparation.Design.Revision,
                    preparation.Fingerprint,
                    pageIds,
                    excluded));
            },
            failure => AppResult<ReleasePreparation>.Fail(failure.Error));
    }
}
