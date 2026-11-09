using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.DesignDocuments.Queries;

/// <summary>Represents a query to list the organization's editor documents without their snapshots.</summary>
public sealed record DesignDocumentGetAllQuery : IQuery<AppResult<IReadOnlyList<DesignDocumentSummary>>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the kind to keep.</summary>
    public string? Kind { get; init; }

    /// <summary>Gets whether archived documents are listed.</summary>
    public required bool IncludeArchived { get; init; }

    /// <summary>Gets the largest number of documents to return.</summary>
    public required int Limit { get; init; }

    /// <summary>Gets the number of documents to skip.</summary>
    public required int Offset { get; init; }
}
