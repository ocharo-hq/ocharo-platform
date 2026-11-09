using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.DesignDocuments.Commands;

/// <summary>Represents a command to replace an editor document's content at an expected revision.</summary>
public sealed record DesignDocumentSaveCommand : ICommand<AppResult<DesignDocument>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the document to replace.</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>Gets the revision the editor opened.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the document's title and snapshot.</summary>
    public required DesignDocumentInput Input { get; init; }
}
