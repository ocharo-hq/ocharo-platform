using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.DesignDocuments.Commands;

/// <summary>Represents a command to archive an editor document at an expected revision.</summary>
public sealed record DesignDocumentArchiveCommand : ICommand<AppResult<DesignDocument>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the document to archive.</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>Gets the revision the editor opened.</summary>
    public required int ExpectedRevision { get; init; }
}
