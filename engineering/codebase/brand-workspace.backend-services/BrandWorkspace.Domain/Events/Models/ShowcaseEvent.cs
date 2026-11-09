namespace BrandWorkspace.Domain.Events.Models;

/// <summary>An event where Ocharo could show its products, with its dates, terms and Ocharo's standing.</summary>
public sealed record ShowcaseEvent(
    Guid Id,
    string? Key,
    string Title,
    string? Organizer,
    string Scope,
    string Format,
    string? Country,
    string? City,
    string? Venue,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string DatePrecision,
    DateOnly? ApplicationDeadline,
    string Participation,
    string? Cost,
    string? Website,
    string? SourceUrl,
    string Status,
    string Priority,
    string Notes,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
