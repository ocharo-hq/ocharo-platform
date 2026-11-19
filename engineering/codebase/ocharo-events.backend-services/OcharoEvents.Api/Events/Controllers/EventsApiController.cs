using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcharoEvents.Api.Access.Filters;
using OcharoEvents.Api.Events.Models;
using OcharoEvents.Application.Events.Errors;
using OcharoEvents.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoEvents.Api.Events.Controllers;

/// <summary>
/// Exposes the Events page's reads and writes: WoW2 Events' tracking, event, proposal, workflow, calendar and intake
/// freshness routes, mirrored under the organization so the page's <c>fe-events</c> client reaches them with
/// <c>{prefix}/api/events/v1/…</c>, its prefix being <c>/api/organizations/{organizationId}/events-api</c> (OE07), as the
/// digest routes are mirrored for the Telegram delivery page. Each route passes its query or body to Events as the kit
/// sent it and answers Events' model in the SDK envelope, so no wire of Ocharo's own sits between the page and Events.
/// </summary>
/// <remarks>Reads need <c>events:read</c> (the organization filter); tracking writes and proposals <c>events:track</c>;
/// the workflow write <c>events:admin</c>; a person without one answers 403 before Events, which checks again. Another
/// organization, or a catalog other than the page's, answers 404.</remarks>
[ApiController]
[Route("api/organizations/{organizationId:guid}/events-api/api/events/v1")]
[Authorize]
[OrganizationAccess]
public sealed class EventsApiController(EventsApiService events, TimeProvider time) : ControllerBase
{
    /// <summary>Holds the calendar file's media type.</summary>
    private const string CalendarMediaType = "text/calendar; charset=utf-8";

    /// <summary>Lists the catalog's published events with the organization's tracking: one Events call with the page's
    /// filters and cursor.</summary>
    [HttpGet("catalogs/{catalog}/tracking")]
    public async Task<IActionResult> ListTracking(
        string catalog, [FromQuery] TrackingQueryParameters parameters, CancellationToken cancellationToken) =>
        parameters.TryMap(out var query) is { } invalid
            ? this.ToProblem(invalid)
            : Answer(await events.ListTrackingAsync(catalog, query, cancellationToken));

    /// <summary>Counts the organization's tracking per stage and lists the nearest open deadlines of the kinds named.</summary>
    [HttpGet("catalogs/{catalog}/tracking/summary")]
    public async Task<IActionResult> TrackingSummary(
        string catalog, [FromQuery] string? today, [FromQuery] string[]? deadlineKinds, CancellationToken cancellationToken)
    {
        if (EventsQueryValues.TryDay("today", today, out var day) is { } invalidDay)
            return this.ToProblem(invalidDay);
        var kinds = new List<DeadlineKind>();
        foreach (var name in deadlineKinds ?? [])
        {
            if (EventsQueryValues.TryEnum<DeadlineKind>("deadlineKinds", name, out var kind) is { } invalidKind)
                return this.ToProblem(invalidKind);
            if (kind is { } parsed) kinds.Add(parsed);
        }
        var query = new TrackingSummaryQuery
        {
            Today = day ?? DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime),
            DeadlineKinds = kinds,
        };
        return Answer(await events.GetTrackingSummaryAsync(catalog, query, cancellationToken));
    }

    /// <summary>Gets one event with the organization's tracking.</summary>
    [HttpGet("catalogs/{catalog}/tracking/{eventId:guid}")]
    public async Task<IActionResult> GetTracking(string catalog, Guid eventId, CancellationToken cancellationToken) =>
        Answer(await events.GetTrackingAsync(catalog, eventId, cancellationToken));

    /// <summary>Starts or changes the organization's tracking of an event at the revision the page read.</summary>
    [HttpPut("catalogs/{catalog}/tracking/{eventId:guid}")]
    public async Task<IActionResult> PutTracking(
        string catalog, Guid eventId, [FromBody] TrackingSpec spec, CancellationToken cancellationToken) =>
        RefuseUntracked() ?? Answer(await events.PutTrackingAsync(catalog, eventId, spec, cancellationToken));

    /// <summary>Stops tracking an event at the revision the page read.</summary>
    [HttpDelete("catalogs/{catalog}/tracking/{eventId:guid}")]
    public async Task<IActionResult> DeleteTracking(
        string catalog, Guid eventId, [FromQuery] int? expectedRevision, CancellationToken cancellationToken)
    {
        if (RefuseUntracked() is { } refused) return refused;
        if (expectedRevision is not { } revision)
            return this.ToProblem(OcharoEventsErrors.Invalid("expectedRevision: required."));
        return Answer(await events.DeleteTrackingAsync(catalog, eventId, revision, cancellationToken));
    }

    /// <summary>Lists the stage changes of the organization's tracking of an event, oldest first.</summary>
    [HttpGet("catalogs/{catalog}/tracking/{eventId:guid}/history")]
    public async Task<IActionResult> TrackingHistory(string catalog, Guid eventId, CancellationToken cancellationToken) =>
        Answer(await events.GetTrackingHistoryAsync(catalog, eventId, cancellationToken));

    /// <summary>Gets one event of the catalog.</summary>
    [HttpGet("catalogs/{catalog}/events/{id:guid}")]
    public async Task<IActionResult> GetEvent(string catalog, Guid id, CancellationToken cancellationToken) =>
        Answer(await events.GetEventAsync(catalog, id, cancellationToken));

    /// <summary>Proposes an event; Events publishes a member's proposal in Ocharo's private catalog at once (OE03).</summary>
    [HttpPost("catalogs/{catalog}/candidates")]
    public async Task<IActionResult> Propose(string catalog, [FromBody] EventSpec spec, CancellationToken cancellationToken) =>
        RefuseUntracked() ?? Answer(await events.ProposeEventAsync(catalog, spec, cancellationToken), StatusCodes.Status201Created);

    /// <summary>Gets the organization's workflow: its stages, default stage and custom fields.</summary>
    [HttpGet("workflow")]
    public async Task<IActionResult> GetWorkflow(CancellationToken cancellationToken) =>
        Answer(await events.GetWorkflowAsync(cancellationToken));

    /// <summary>Writes the organization's workflow at the revision the caller read, for an admin's own edit. No page calls
    /// it: the deploy writes Ocharo's stage labels, classes and custom fields with Events' <c>workflow put --spec</c>.</summary>
    [HttpPut("workflow")]
    public async Task<IActionResult> PutWorkflow([FromBody] WorkflowSpec spec, CancellationToken cancellationToken) =>
        OrganizationAccessFilter.GetAccess(HttpContext).CanAdmin
            ? Answer(await events.PutWorkflowAsync(spec, cancellationToken))
            : this.ToProblem(OcharoEventsErrors.Forbidden("Only the organization's admins change its event stages."));

    /// <summary>Exports the catalog's published events the filters select as an iCalendar file, no envelope.</summary>
    [HttpGet("catalogs/{catalog}/events.ics")]
    public async Task<IActionResult> ExportCalendar(
        string catalog, [FromQuery] EventQueryParameters parameters, CancellationToken cancellationToken)
    {
        if (parameters.TryMap(out var query) is { } invalid)
            return this.ToProblem(invalid);
        return await events.ExportCalendarAsync(catalog, query, cancellationToken) switch
        {
            Result<CalendarFile>.Success { Value: var file } =>
                File(Encoding.UTF8.GetBytes(file.Content), CalendarMediaType, file.FileName),
            Result<CalendarFile>.Failure failure => this.ToProblem(failure.Error),
            _ => throw new InvalidOperationException("A result is a success or a failure."),
        };
    }

    /// <summary>Gets how fresh the catalog's intake is: when its sources last received records, the newest observed time and
    /// how many sources fail (E24). Events reads it for the signed-in member's organization, so the page can tell a quiet
    /// week from a stalled pull.</summary>
    [HttpGet("catalogs/{catalog}/freshness")]
    public async Task<IActionResult> Freshness(string catalog, CancellationToken cancellationToken) =>
        Answer(await events.GetIntakeFreshnessAsync(catalog, cancellationToken));

    /// <summary>Refuses a tracking write or a proposal by a person without <c>events:track</c>.</summary>
    private IActionResult? RefuseUntracked() =>
        OrganizationAccessFilter.GetAccess(HttpContext).CanTrack
            ? null
            : this.ToProblem(OcharoEventsErrors.Forbidden("An editor or owner role is required."));

    /// <summary>Answers the call in the SDK envelope, or its failure as a problem.</summary>
    private IActionResult Answer<T>(Result<T> result, int status = StatusCodes.Status200OK) where T : notnull =>
        result switch
        {
            Result<T>.Success { Value: var value } => StatusCode(status, ApiResponse<T>.Ok(value)),
            Result<T>.Failure failure => this.ToProblem(failure.Error),
            _ => throw new InvalidOperationException("A result is a success or a failure."),
        };
}
