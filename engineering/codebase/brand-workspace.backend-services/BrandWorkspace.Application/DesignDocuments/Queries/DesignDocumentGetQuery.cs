using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.DesignDocuments.Queries;

/// <summary>Represents a query to get an editor document at its current revision.</summary>
public sealed record DesignDocumentGetQuery : IQuery<AppResult<DesignDocument>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the document to read.</summary>
    public required Guid DocumentId { get; init; }
}
