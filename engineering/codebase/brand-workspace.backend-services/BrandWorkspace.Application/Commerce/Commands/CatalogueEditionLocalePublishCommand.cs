using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Commerce.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Commands;

/// <summary>Represents a command to publish a language's catalogue edition at an expected draft revision.</summary>
public sealed record CatalogueEditionLocalePublishCommand : ICommand<AppResult<CommerceRelease>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }

    /// <summary>Gets the draft revision the operator saw.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the edition's language.</summary>
    public required string Locale { get; init; }

    /// <summary>Gets the reason for publishing.</summary>
    public required string Note { get; init; }
}
