using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Events.Commands;

/// <summary>Represents a command to record a showcase event.</summary>
public sealed record EventCreateCommand : ICommand<AppResult<ShowcaseEvent>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the fields as the operator sent them.</summary>
    public required EventFields Fields { get; init; }
}
