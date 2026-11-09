using System.Text.Json;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Designs.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.QueryHandlers;

/// <summary>Handles <see cref="DesignRevisionGetByIdQuery"/>.</summary>
public sealed class DesignRevisionGetByIdQueryHandler(IDesignRepository designs)
    : IQueryHandler<DesignRevisionGetByIdQuery, AppResult<CatalogueDesignDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CatalogueDesignDetail>> HandleAsync(
        DesignRevisionGetByIdQuery request,
        CancellationToken ct)
    {
        var current = await designs.GetAsync(request.Id, ct);
        if (current is null)
        {
            return AppResult<CatalogueDesignDetail>.Fail(CatalogueErrors.NotFound("Catalogue design"));
        }

        var revision = await designs.RevisionAsync(
            request.Id,
            request.Revision,
            ct);
        if (revision is null)
        {
            return AppResult<CatalogueDesignDetail>.Fail(CatalogueErrors.NotFound("Catalogue revision"));
        }

        var detail = new CatalogueDesignDetail(
            request.Id,
            revision.Title,
            revision.Revision,
            current.Archived,
            current.CreatedAtUtc,
            revision.SavedAtUtc,
            CatalogueJson.Read<JsonElement>(revision.Json));
        return AppResult<CatalogueDesignDetail>.Ok(detail);
    }
}
