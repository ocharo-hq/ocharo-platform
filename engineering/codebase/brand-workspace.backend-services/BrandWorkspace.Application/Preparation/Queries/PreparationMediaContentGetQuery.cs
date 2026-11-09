using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Queries;

/// <summary>Represents a query to read a preparation image's bytes.</summary>
public sealed record PreparationMediaContentGetQuery : IQuery<AppResult<MediaContent>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the image to read.</summary>
    public required Guid MediaId { get; init; }
}
