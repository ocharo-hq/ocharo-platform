using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Collections.Commands;

/// <summary>Represents a command to delete a collection; it answers the deleted collection's id.</summary>
public sealed record CollectionDeleteCommand : ICommand<AppResult<string>>
{
    /// <summary>Gets the collection id.</summary>
    public required string Id { get; init; }
}
