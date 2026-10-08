using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Preparation;
using BrandWorkspace.Infrastructure.Events.Services;

namespace BrandWorkspace.Api.Events;

public sealed record EventRequest(
    string? Title, string? Organizer, string? Scope, string? Format, string? Country, string? City, string? Venue,
    string? StartsOn, string? EndsOn, string? DatePrecision, string? ApplicationDeadline, string? Participation, string? Cost,
    string? Website, string? SourceUrl, string? Status, string? Priority, string? Notes)
{
    public EventFields Fields() => new(
        Title, Organizer, Scope, Format, Country, City, Venue, StartsOn, EndsOn, DatePrecision, ApplicationDeadline, Participation,
        Cost, Website, SourceUrl, Status, Priority, Notes);
}

public sealed record UpdateEventRequest(
    int ExpectedRevision, string? Title, string? Organizer, string? Scope, string? Format, string? Country, string? City,
    string? Venue, string? StartsOn, string? EndsOn, string? DatePrecision, string? ApplicationDeadline, string? Participation,
    string? Cost, string? Website, string? SourceUrl, string? Status, string? Priority, string? Notes)
{
    public EventFields Fields() => new(
        Title, Organizer, Scope, Format, Country, City, Venue, StartsOn, EndsOn, DatePrecision, ApplicationDeadline, Participation,
        Cost, Website, SourceUrl, Status, Priority, Notes);
}

/// <summary>Maps the showcase events feed behind organization membership and antiforgery.</summary>
public static class EventEndpoints
{
    public static WebApplication MapEventEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/organizations/{organizationId:guid}/events")
            .RequireAuthorization()
            .AddEndpointFilter<OrganizationAccessFilter>()
            .AddEndpointFilter<PreparationErrorFilter>();

        group.MapGet("/", async (string? query, string? scope, string? format, string? status, string? from, string? to,
                bool? includeUndated, int? limit, int? offset, HttpContext context, EventService service, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.GetEventsAsync(Access(context), query, scope, format, status, from, to, includeUndated ?? true,
                    limit ?? 200, offset ?? 0, token)
            }));
        group.MapGet("/summary", async (string? today, HttpContext context, EventService service, CancellationToken token) =>
            Results.Ok(new { data = await service.GetSummaryAsync(Access(context), today, token) }));
        group.MapPost("/", async (EventRequest request, HttpContext context, EventService service,
                EventValidationService validation, CancellationToken token) =>
            Results.Ok(new { data = await service.CreateEventAsync(Access(context), validation.Event(request.Fields()), token) }));
        group.MapGet("/{eventId:guid}", async (Guid eventId, HttpContext context, EventService service, CancellationToken token) =>
            Results.Ok(new { data = await service.GetEventAsync(Access(context), eventId, token) }));
        group.MapPut("/{eventId:guid}", async (Guid eventId, UpdateEventRequest request, HttpContext context, EventService service,
                EventValidationService validation, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.UpdateEventAsync(Access(context), eventId, request.ExpectedRevision,
                    validation.Event(request.Fields()), token)
            }));
        group.MapDelete("/{eventId:guid}", async (Guid eventId, int expectedRevision, HttpContext context, EventService service,
            CancellationToken token) =>
        {
            await service.DeleteEventAsync(Access(context), eventId, expectedRevision, token);
            return Results.Ok(new { data = new { id = eventId } });
        });
        return app;
    }

    private static Application.Access.OrganizationAccess Access(HttpContext context) => OrganizationAccessFilter.GetAccess(context);
}
