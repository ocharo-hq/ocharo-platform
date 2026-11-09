using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Commands;

/// <summary>Represents a command to set one status on several products.</summary>
public sealed record ProductStatusBulkSetCommand : ICommand<AppResult<BulkResult>>
{
    /// <summary>Gets the products and their new status.</summary>
    public required BulkStatusInput Input { get; init; }
}
