using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Files.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Queries;

/// <summary>Represents a query to open an export's ZIP: a WoW2 Files link, or its bytes.</summary>
public sealed record ExportOpenQuery : IQuery<AppResult<StoredContent>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the export to open.</summary>
    public required Guid ExportId { get; init; }
}
