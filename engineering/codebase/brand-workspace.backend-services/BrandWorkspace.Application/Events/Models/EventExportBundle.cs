namespace BrandWorkspace.Application.Events.Models;

/// <summary>
/// Desk's events as WoW2 Events imports them: <c>wow2.events-import.v1</c>, Events' contract § Imports
/// (events-adoption.md #6). The importing caller's token names the organization, so the bundle carries none.
/// </summary>
/// <param name="Format">The bundle format.</param>
/// <param name="Source">The producing system, <c>ocharo-desk</c>; it prefixes each row's source identity.</param>
/// <param name="Rows">One row per stored event, oldest first.</param>
public sealed record EventExportBundle(string Format, string Source, IReadOnlyList<ExportedEventRow> Rows);

/// <summary>One stored event: its facts, Ocharo's tracking, and the whole Desk row as evidence.</summary>
/// <param name="LegacyId">The event's Desk id; Events' old → new id map keys on it, so a rerun finds the same event.</param>
/// <param name="Facts">The catalog facts, dates at the precision and certainty Desk stored.</param>
/// <param name="SourceUrl">Where the event was found.</param>
/// <param name="RecordedAtUtc">When Desk first recorded it.</param>
/// <param name="Legacy">The whole stored row, for reconciliation and rollback.</param>
/// <param name="Tracking">Ocharo's standing with the event.</param>
public sealed record ExportedEventRow(
    string LegacyId,
    ExportedFacts Facts,
    string? SourceUrl,
    DateTimeOffset RecordedAtUtc,
    ShowcaseEventLegacyRow Legacy,
    ExportedTracking Tracking);

/// <summary>An event's facts in Events' shape.</summary>
/// <param name="Key">The import slug, when the event came from a bundle.</param>
/// <param name="Title">The title per locale; Desk stored no language, so only <c>und</c>.</param>
/// <param name="Kind">Desk's <c>format</c>, a key of the catalog's taxonomy.</param>
/// <param name="Organizer">The organizer, when stored.</param>
/// <param name="Place">Venue, city and country, when any is stored.</param>
/// <param name="Starts">The start; <c>unknown</c> with no value when Desk stored no date.</param>
/// <param name="Ends">The end, as precise and as certain as the start; null when not stored.</param>
/// <param name="Deadlines">The application deadline, when stored.</param>
/// <param name="Website">The official page.</param>
public sealed record ExportedFacts(
    string? Key,
    IReadOnlyDictionary<string, string> Title,
    string Kind,
    ExportedOrganizer? Organizer,
    ExportedPlace? Place,
    ExportedWhen Starts,
    ExportedWhen? Ends,
    IReadOnlyList<ExportedDeadline> Deadlines,
    string? Website);

/// <summary>Who runs an event; Desk stores only the name.</summary>
public sealed record ExportedOrganizer(string Name, string? Website);

/// <summary>Where an event takes place.</summary>
public sealed record ExportedPlace(string? Venue, string? City, string? Country);

/// <summary>
/// A date in Events' shape: <c>yyyy-MM-dd</c> at <c>day</c>, <c>yyyy-MM</c> at <c>month</c>, no value at
/// <c>unknown</c>; certainty <c>announced</c>, or <c>estimated</c> when inferred from earlier editions.
/// </summary>
public sealed record ExportedWhen(string? Value, string Precision, string Certainty);

/// <summary>A deadline of one kind, such as <c>application</c>.</summary>
public sealed record ExportedDeadline(string Kind, ExportedWhen At);

/// <summary>An organization's standing with an event; <c>Fields</c> holds what Events has no column for.</summary>
public sealed record ExportedTracking(string Stage, string Priority, string Notes, ExportedTrackingFields Fields);

/// <summary>Desk's tracking fields Events keeps as the organization's own.</summary>
public sealed record ExportedTrackingFields(string Participation, string? Cost);

/// <summary>Every column of one <c>events.showcase_events</c> row, as stored.</summary>
public sealed record ShowcaseEventLegacyRow(
    Guid OrganizationId,
    Guid Id,
    string? ImportKey,
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
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>What an export wrote: counts the reconciliation compares against the import's report.</summary>
public sealed record EventExportReport(
    Guid OrganizationId,
    string Bundle,
    int Count,
    int Keyed,
    IReadOnlyDictionary<string, int> ByStage,
    IReadOnlyDictionary<string, int> ByPrecision);
