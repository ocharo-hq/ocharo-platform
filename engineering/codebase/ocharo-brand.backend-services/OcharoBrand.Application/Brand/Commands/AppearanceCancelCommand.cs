using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Commands;

/// <summary>Represents a command to cancel a scheduled appearance.</summary>
public sealed record AppearanceCancelCommand : ICommand<AppResult<BrandAppearance>>
{
    /// <summary>Gets the appearance to cancel.</summary>
    public required string Id { get; init; }
}
