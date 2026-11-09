using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.DesignDocuments.Queries;

/// <summary>Represents a query to list an editor document's saved revisions.</summary>
public sealed record DesignDocumentRevisionGetAllQuery : IQuery<AppResult<IReadOnlyList<DesignDocumentRevision>>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the document whose revisions to list.</summary>
    public required Guid DocumentId { get; init; }

    /// <summary>Gets the largest number of revisions to return.</summary>
    public required int Limit { get; init; }

    /// <summary>Gets the number of revisions to skip.</summary>
    public required int Offset { get; init; }
}
