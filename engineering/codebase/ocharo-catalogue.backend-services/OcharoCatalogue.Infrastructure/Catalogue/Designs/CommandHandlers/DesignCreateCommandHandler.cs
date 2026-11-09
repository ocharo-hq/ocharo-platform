using OcharoCatalogue.Application.Catalogue.Designs.Commands;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.CommandHandlers;

/// <summary>Handles <see cref="DesignCreateCommand"/>.</summary>
public sealed class DesignCreateCommandHandler(DesignSaveService designs)
    : ICommandHandler<DesignCreateCommand, AppResult<CatalogueDesignDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CatalogueDesignDetail>> HandleAsync(
        DesignCreateCommand request,
        CancellationToken ct)
    {
        return await designs.SaveAsync(
            null,
            request.Input,
            ct);
    }
}
