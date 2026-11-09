using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Designs.Queries;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.QueryHandlers;

/// <summary>Handles <see cref="DesignGetByIdQuery"/>.</summary>
public sealed class DesignGetByIdQueryHandler(IDesignRepository designs)
    : IQueryHandler<DesignGetByIdQuery, AppResult<CatalogueDesignDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CatalogueDesignDetail>> HandleAsync(
        DesignGetByIdQuery request,
        CancellationToken ct)
    {
        var record = await designs.GetAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<CatalogueDesignDetail>.Fail(CatalogueErrors.NotFound("Catalogue design"));
        }

        return AppResult<CatalogueDesignDetail>.Ok(DesignProjections.ToDetail(record));
    }
}
