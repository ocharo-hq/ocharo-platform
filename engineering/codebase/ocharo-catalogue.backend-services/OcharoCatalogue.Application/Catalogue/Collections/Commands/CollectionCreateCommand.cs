using OcharoCatalogue.Application.Catalogue.Collections.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Collections.Commands;

/// <summary>Represents a command to create a collection.</summary>
public sealed record CollectionCreateCommand : ICommand<AppResult<CollectionDetail>>
{
    /// <summary>Gets the collection fields.</summary>
    public required CollectionInput Input { get; init; }
}
