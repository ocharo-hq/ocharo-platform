using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Queries;

/// <summary>Represents a query to count the organization's supplier media and its inbox.</summary>
public sealed record SourcingMediaTotalsGetQuery : IQuery<AppResult<SourcingMediaTotals>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }
}
