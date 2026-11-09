using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.DesignDocuments.Commands;

/// <summary>Represents a command to create an editor document.</summary>
public sealed record DesignDocumentCreateCommand : ICommand<AppResult<DesignDocument>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the document's kind.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets the document's title and snapshot.</summary>
    public required DesignDocumentInput Input { get; init; }
}
