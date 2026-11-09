using BrandWorkspace.Application.Access.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Commands;

/// <summary>Represents a command to withdraw a brand's release, with a reason.</summary>
public sealed record ReleaseWithdrawCommand : ICommand<AppResult<Guid>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }

    /// <summary>Gets the release to withdraw.</summary>
    public required Guid ReleaseId { get; init; }

    /// <summary>Gets the reason for withdrawing.</summary>
    public required string Reason { get; init; }
}
