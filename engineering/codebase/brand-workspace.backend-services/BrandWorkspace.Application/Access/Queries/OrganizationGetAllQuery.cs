using BrandWorkspace.Application.Access.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Access.Queries;

/// <summary>Represents a query to list the organizations the signed-in person's session opens: its active organization, projected into Desk.</summary>
public sealed record OrganizationGetAllQuery : IQuery<AppResult<IReadOnlyList<Organization>>>
{
    /// <summary>Gets the session's active organization, if any.</summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>Gets the person's role in it, if any.</summary>
    public string? Role { get; init; }

    /// <summary>Gets the organization's name as Identity names it.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the organization's slug as Identity names it.</summary>
    public string? Slug { get; init; }
}
