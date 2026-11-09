using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Preparation.Queries;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.QueryHandlers;

/// <summary>Handles <see cref="ExportOpenQuery"/>.</summary>
public sealed class ExportOpenQueryHandler(PreparationService service)
    : IQueryHandler<ExportOpenQuery, AppResult<StoredContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<StoredContent>> HandleAsync(
        ExportOpenQuery request,
        CancellationToken cancellationToken)
    {
        Result<StoredContent> result = await service.OpenExportAsync(
            request.Access,
            request.ExportId,
            cancellationToken);
        return result.ToAppResult();
    }
}
