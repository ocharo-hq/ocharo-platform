using OcharoCatalogue.Application.Catalogue.Collections.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Collections.Commands;

/// <summary>Represents a command to replace a collection at the revision the caller edited.</summary>
public sealed record CollectionUpdateCommand : ICommand<AppResult<CollectionDetail>>
{
    /// <summary>Gets the collection id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the collection fields.</summary>
    public required CollectionInput Input { get; init; }
}
