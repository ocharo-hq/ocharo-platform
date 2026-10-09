using System.ComponentModel;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace BrandWorkspace.Api.Mcp;

/// <summary>Reads supplier evidence and events from the organization bound to the current MCP ticket.</summary>
[McpServerToolType]
[Authorize(Policy = DeskMcpEndpoints.Policy)]
public sealed class DeskReadTools(
    IHttpContextAccessor http,
    SourcingService sourcing,
    EventService events,
    ILogger<DeskReadTools> logger)
{
    /// <summary>Lists material suppliers without contacting or changing them.</summary>
    [McpServerTool(Name = "list_suppliers", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List stored material suppliers in the connected organization. These are recorded leads, not endorsements or verified offers. Bounded to 200 rows; use offset for pagination.")]
    public Task<IReadOnlyList<SupplierSummary>> ListSuppliers(
        string? query = null, string? stage = null, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        return ReadAsync(access => sourcing.GetSuppliersAsync(access, query, stage, limit, offset, cancellationToken));
    }

    /// <summary>Reads one supplier's recorded people, contact routes and notes.</summary>
    [McpServerTool(Name = "get_supplier", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read a stored supplier by ID returned by list_suppliers. Includes private contact evidence and notes; never sends a message.")]
    public Task<Supplier> GetSupplier(Guid supplierId, CancellationToken cancellationToken = default)
    {
        return ReadAsync(access => sourcing.GetSupplierAsync(access, supplierId, cancellationToken));
    }

    /// <summary>Lists immutable media metadata and provenance, without file bytes or external retrieval.</summary>
    [McpServerTool(Name = "list_supplier_media", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List stored supplier media metadata and source provenance. Does not return original files, GPS coordinates or download content. Limit is at most 200. Set unassigned for the inbox, or supplierId, not both.")]
    public Task<IReadOnlyList<SourcingMedia>> ListSupplierMedia(
        Guid? supplierId = null, bool unassigned = false, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
    {
        return ReadAsync(access => sourcing.GetMediaAsync(access, supplierId, unassigned, limit, offset, cancellationToken));
    }

    /// <summary>Lists event candidates with their recorded dates and review status.</summary>
    [McpServerTool(Name = "list_events", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("List stored events in the connected organization. Dates marked month or estimated are not exact dates. Source facts may need rechecking. ISO from/to filters and bounded pagination are supported; limit is at most 500.")]
    public Task<IReadOnlyList<ShowcaseEvent>> ListEvents(
        string? query = null, string? scope = null, string? format = null, string? status = null,
        string? from = null, string? to = null, bool includeUndated = true, int limit = 50, int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return ReadAsync(access => events.GetEventsAsync(access, query, scope, format, status, from, to,
            includeUndated, limit, offset, cancellationToken));
    }

    /// <summary>Reads one event's recorded participation terms and source evidence.</summary>
    [McpServerTool(Name = "get_event", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read a stored event by ID returned by list_events. Unknown fees remain unknown. Does not register, apply, pay or visit source URLs.")]
    public Task<ShowcaseEvent> GetEvent(Guid eventId, CancellationToken cancellationToken = default)
    {
        return ReadAsync(access => events.GetEventAsync(access, eventId, cancellationToken));
    }

    /// <summary>Reads event status counts and the nearest recorded application deadlines.</summary>
    [McpServerTool(Name = "get_event_summary", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Read event status totals and up to five upcoming application deadlines. Optional today is an ISO date; otherwise server UTC date is used. This does not create calendar events or reminders.")]
    public Task<ShowcaseEventSummary> GetEventSummary(string? today = null, CancellationToken cancellationToken = default)
    {
        return ReadAsync(access => events.GetSummaryAsync(access, today, cancellationToken));
    }

    private async Task<T> ReadAsync<T>(Func<OrganizationAccess, Task<T>> read)
    {
        try
        {
            HttpContext context = http.HttpContext ?? throw new InvalidOperationException("No MCP request is active.");
            OrganizationAccess access = DeskMcpAuthenticationHandler.GetAccess(context);
            return await read(access);
        }
        catch (PreparationException exception)
        {
            throw new McpException(exception.Code == "not-found" ? "The record was not found in this organization." : exception.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Desk MCP read failed");
            throw new McpException("Desk could not complete this read. Retry or inspect the operator console.");
        }
    }
}
