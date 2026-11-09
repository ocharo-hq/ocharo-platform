using OcharoCatalogue.Application.Catalogue.Collections.Commands;
using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Collections.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Collections.CommandHandlers;

/// <summary>Handles <see cref="CollectionCreateCommand"/>.</summary>
public sealed class CollectionCreateCommandHandler(CollectionSaveService collections)
    : ICommandHandler<CollectionCreateCommand, AppResult<CollectionDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CollectionDetail>> HandleAsync(
        CollectionCreateCommand request,
        CancellationToken ct)
    {
        return await collections.SaveAsync(
            null,
            request.Input,
            ct);
    }
}
