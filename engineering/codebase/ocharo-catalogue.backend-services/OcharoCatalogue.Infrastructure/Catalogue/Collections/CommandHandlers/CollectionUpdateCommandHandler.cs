using OcharoCatalogue.Application.Catalogue.Collections.Commands;
using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Collections.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Collections.CommandHandlers;

/// <summary>Handles <see cref="CollectionUpdateCommand"/>.</summary>
public sealed class CollectionUpdateCommandHandler(CollectionSaveService collections)
    : ICommandHandler<CollectionUpdateCommand, AppResult<CollectionDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CollectionDetail>> HandleAsync(
        CollectionUpdateCommand request,
        CancellationToken ct)
    {
        return await collections.SaveAsync(
            request.Id,
            request.Input,
            ct);
    }
}
