using BrandWorkspace.Domain.Events.Models;

namespace BrandWorkspace.Application.Events.Models;

/// <summary>
/// Desk's events as WoW2 Events imports them (<c>wow2.events-import.v1</c>, events-adoption.md #6): one record per
/// stored event, keyed by its Desk id so the import's old → new id map stays stable across reruns.
/// </summary>
/// <param name="Format">The bundle format.</param>
/// <param name="Source">Where and when the bundle was exported.</param>
/// <param name="Events">The exported events, oldest first.</param>
public sealed record EventExportBundle(string Format, EventExportSource Source, IReadOnlyList<ExportedEvent> Events);

/// <summary>The system and organization a bundle comes from.</summary>
/// <param name="System">The exporting system, <c>ocharo-desk</c>.</param>
/// <param name="OrganizationId">The organization in that system, which the import's target organization replaces.</param>
/// <param name="ExportedAt">When the export ran.</param>
/// <param name="Count">How many events the bundle holds.</param>
public sealed record EventExportSource(string System, Guid OrganizationId, DateTimeOffset ExportedAt, int Count);

/// <summary>One exported event: catalog facts, the organization's tracking, provenance and the stored row.</summary>
/// <param name="SourceId">The event's Desk id; the import maps it to exactly one new id.</param>
/// <param name="Key">The import slug, when the event came from a bundle.</param>
/// <param name="Title">The title with its language; Desk stored none, so it is <c>und</c>.</param>
/// <param name="Organizer">The organizer.</param>
/// <param name="Kind">Desk's <c>format</c>.</param>
/// <param name="Place">Country, city and venue.</param>
/// <param name="Starts">The start, with its precision and certainty; null when the dates are unknown.</param>
/// <param name="Ends">The end, as precise and as certain as the start; null when not stored.</param>
/// <param name="Deadlines">The application deadline, when stored.</param>
/// <param name="Website">The official page.</param>
/// <param name="Tracking">Ocharo's standing with the event.</param>
/// <param name="Provenance">Where the record came from in Desk.</param>
/// <param name="Legacy">The stored row as Desk's contract reads it, <c>scope</c> included, for reconciliation.</param>
public sealed record ExportedEvent(
    Guid SourceId,
    string? Key,
    ExportedText Title,
    string? Organizer,
    string Kind,
    ExportedPlace Place,
    ExportedDate? Starts,
    ExportedDate? Ends,
    IReadOnlyList<ExportedDeadline> Deadlines,
    string? Website,
    ExportedTracking Tracking,
    ExportedProvenance Provenance,
    ShowcaseEvent Legacy);

/// <summary>A text with its BCP 47 language.</summary>
public sealed record ExportedText(string Value, string Language);

/// <summary>Where an event takes place.</summary>
public sealed record ExportedPlace(string? Country, string? City, string? Venue);

/// <summary>
/// A date at the precision Desk stored: <c>day</c> or <c>month</c> (the 1st of that month); certainty
/// <c>announced</c>, or <c>estimated</c> when inferred from earlier editions, which never becomes announced.
/// </summary>
public sealed record ExportedDate(DateOnly Date, string Precision, string Certainty);

/// <summary>A deadline of one kind, such as <c>application</c>.</summary>
public sealed record ExportedDeadline(string Kind, DateOnly Date);

/// <summary>An organization's standing with an event.</summary>
public sealed record ExportedTracking(string Stage, string Priority, string Notes, string Participation, string? Cost);

/// <summary>Where a record came from.</summary>
public sealed record ExportedProvenance(
    string System,
    Guid OrganizationId,
    Guid EventId,
    string? Key,
    string? SourceUrl,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Revision);

/// <summary>What an export wrote: counts the reconciliation compares against the import's.</summary>
public sealed record EventExportReport(
    Guid OrganizationId,
    string Bundle,
    int Count,
    int Keyed,
    IReadOnlyDictionary<string, int> ByStage,
    IReadOnlyDictionary<string, int> ByPrecision);
