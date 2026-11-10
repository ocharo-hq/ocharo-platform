using OcharoEvents.Application.Access.Models;
using OcharoEvents.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Application.Events.Queries;

/// <summary>Represents a query to list the organization's showcase events, filtered and paged.</summary>
public sealed record EventGetAllQuery : IQuery<AppResult<IReadOnlyList<ShowcaseEvent>>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the text to search for.</summary>
    public string? Query { get; init; }

    /// <summary>Gets the scope to keep.</summary>
    public string? Scope { get; init; }

    /// <summary>Gets the format to keep.</summary>
    public string? Format { get; init; }

    /// <summary>Gets the status to keep.</summary>
    public string? Status { get; init; }

    /// <summary>Gets the first ISO date to keep.</summary>
    public string? From { get; init; }

    /// <summary>Gets the last ISO date to keep.</summary>
    public string? To { get; init; }

    /// <summary>Gets whether events without dates match a date range.</summary>
    public required bool IncludeUndated { get; init; }

    /// <summary>Gets the largest number of events to return.</summary>
    public required int Limit { get; init; }

    /// <summary>Gets the number of events to skip.</summary>
    public required int Offset { get; init; }
}
