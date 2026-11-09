using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Commands;

/// <summary>Represents a command to prepare an approved revision's manual package once per retry identity.</summary>
public sealed record ExportCreateCommand : ICommand<AppResult<ManualExport>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the draft to export.</summary>
    public required Guid DraftId { get; init; }

    /// <summary>Gets the approved revision.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the export's retry identity.</summary>
    public required Guid IdempotencyKey { get; init; }
}
