using OcharoCatalogue.Application.Catalogue.Designs.Commands;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.CommandHandlers;

/// <summary>Handles <see cref="DesignUpdateCommand"/>.</summary>
public sealed class DesignUpdateCommandHandler(DesignSaveService designs)
    : ICommandHandler<DesignUpdateCommand, AppResult<CatalogueDesignDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CatalogueDesignDetail>> HandleAsync(
        DesignUpdateCommand request,
        CancellationToken ct)
    {
        return await designs.SaveAsync(
            request.Id,
            request.Input,
            ct);
    }
}
