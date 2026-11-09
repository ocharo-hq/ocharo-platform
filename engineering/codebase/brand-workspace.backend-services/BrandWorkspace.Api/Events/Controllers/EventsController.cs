using BrandWorkspace.Api.Access.Filters;
using BrandWorkspace.Api.Events.Models;
using BrandWorkspace.Api.Foundation.Models;
using BrandWorkspace.Application.Events.Commands;
using BrandWorkspace.Application.Events.Queries;
using BrandWorkspace.Domain.Events.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace BrandWorkspace.Api.Events.Controllers;

/// <summary>Exposes an organization's showcase events over HTTP.</summary>
[ApiController]
[Route("api/organizations/{organizationId:guid}/events")]
[Authorize]
[OrganizationAccess]
public sealed class EventsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the events matching the filters, a page at a time.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? query, [FromQuery] string? scope, [FromQuery] string? format,
        [FromQuery] string? status, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] bool? includeUndated,
        [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new EventGetAllQuery
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            Query = query,
            Scope = scope,
            Format = format,
            Status = status,
            From = from,
            To = to,
            IncludeUndated = includeUndated ?? true,
            Limit = limit ?? 200,
            Offset = offset ?? 0,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<ShowcaseEvent>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the counts per status and the nearest application deadlines.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] string? today, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new EventSummaryGetQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), Today = today }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ShowcaseEventSummary>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Records a new event.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EventRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new EventCreateCommand { Access = OrganizationAccessFilter.GetAccess(HttpContext), Fields = request.Fields() },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ShowcaseEvent>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets one event.</summary>
    [HttpGet("{eventId:guid}")]
    public async Task<IActionResult> Get(Guid eventId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new EventGetQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), EventId = eventId }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ShowcaseEvent>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Replaces an event at the revision the operator edited.</summary>
    [HttpPut("{eventId:guid}")]
    public async Task<IActionResult> Update(Guid eventId, [FromBody] UpdateEventRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new EventUpdateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            EventId = eventId,
            ExpectedRevision = request.ExpectedRevision,
            Fields = request.Fields(),
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ShowcaseEvent>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Deletes an event at the revision the operator saw.</summary>
    [HttpDelete("{eventId:guid}")]
    public async Task<IActionResult> Delete(Guid eventId, [FromQuery] int expectedRevision, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new EventDeleteCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            EventId = eventId,
            ExpectedRevision = expectedRevision,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<RecordDeletedDto>.Ok(new RecordDeletedDto(ok.Data))),
            fail => this.ToProblem(fail.Error));
    }
}
