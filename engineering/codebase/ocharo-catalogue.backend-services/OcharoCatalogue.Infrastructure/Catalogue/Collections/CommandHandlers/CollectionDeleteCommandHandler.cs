using OcharoCatalogue.Application.Catalogue.Collections.Commands;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Collections.CommandHandlers;

/// <summary>Handles <see cref="CollectionDeleteCommand"/>.</summary>
public sealed class CollectionDeleteCommandHandler(ICatalogueRepository repository)
    : ICommandHandler<CollectionDeleteCommand, AppResult<string>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<string>> HandleAsync(CollectionDeleteCommand request, CancellationToken ct)
    {
        if (await repository.FindCollectionAsync(request.Id, ct) is null)
        {
            return AppResult<string>.Fail(CatalogueErrors.NotFound("Collection"));
        }

        await repository.DeleteCollectionAsync(request.Id, ct);
        return AppResult<string>.Ok(request.Id);
    }
}
