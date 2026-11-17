using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcharoEvents.Api.Access.Filters;
using OcharoEvents.Application.Events.Errors;
using OcharoEvents.Infrastructure.Delivery.Services;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoEvents.Api.Delivery.Controllers;

/// <summary>
/// Exposes the organization's Telegram delivery to its admins: WoW2 Events' digest routes, mirrored under the organization
/// so the page's <c>fe-events</c> client reaches them with <c>{prefix}/api/events/v1/catalogs/{catalog}/digests…</c>, its
/// prefix being <c>/api/organizations/{organizationId}/delivery</c> (tasks-platform.md O17). A person without
/// <c>events:admin</c> in the organization answers 403; another organization, or a catalog other than the page's, 404.
/// </summary>
[ApiController]
[Route("api/organizations/{organizationId:guid}/delivery/api/events/v1/catalogs/{catalog}/digests")]
[Authorize]
[OrganizationAccess]
public sealed class DigestsController(EventDigestsService digests) : ControllerBase
{
    /// <summary>Lists the organization's digests.</summary>
    [HttpGet]
    public Task<IActionResult> List(string catalog, CancellationToken cancellationToken) =>
        AnswerAsync(() => digests.ListAsync(catalog, cancellationToken));

    /// <summary>Gets one digest.</summary>
    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(string catalog, Guid id, CancellationToken cancellationToken) =>
        AnswerAsync(() => digests.GetAsync(catalog, id, cancellationToken));

    /// <summary>Creates a digest.</summary>
    [HttpPost]
    public Task<IActionResult> Create(string catalog, [FromBody] DigestSpec spec, CancellationToken cancellationToken) =>
        AnswerAsync(() => digests.CreateAsync(catalog, spec, cancellationToken), StatusCodes.Status201Created);

    /// <summary>Replaces a digest at the revision the page read.</summary>
    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(
        string catalog, Guid id, [FromBody] DigestSpec spec, CancellationToken cancellationToken) =>
        AnswerAsync(() => digests.UpdateAsync(catalog, id, spec, cancellationToken));

    /// <summary>Deletes a digest and its run history.</summary>
    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(
        string catalog, Guid id, [FromQuery] int expectedRevision, CancellationToken cancellationToken) =>
        AnswerAsync(() => digests.DeleteAsync(catalog, id, expectedRevision, cancellationToken));

    /// <summary>Renders the digest now without sending.</summary>
    [HttpPost("{id:guid}/preview")]
    public Task<IActionResult> Preview(string catalog, Guid id, CancellationToken cancellationToken) =>
        AnswerAsync(() => digests.PreviewAsync(catalog, id, cancellationToken));

    /// <summary>Sends the digest now to every recipient.</summary>
    [HttpPost("{id:guid}/send")]
    public Task<IActionResult> Send(string catalog, Guid id, CancellationToken cancellationToken) =>
        AnswerAsync(() => digests.SendNowAsync(catalog, id, cancellationToken));

    /// <summary>Lists the digest's runs, newest first.</summary>
    [HttpGet("{id:guid}/runs")]
    public Task<IActionResult> Runs(
        string catalog, Guid id, [FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        AnswerAsync(() => digests.ListRunsAsync(
            catalog, id, limit is { } size ? new DigestRunQuery { Cursor = cursor, Limit = size } : new DigestRunQuery { Cursor = cursor },
            cancellationToken));

    /// <summary>Refuses a person without <c>events:admin</c>, then answers the call in the SDK envelope or as a problem.</summary>
    private async Task<IActionResult> AnswerAsync<T>(Func<Task<Result<T>>> call, int status = StatusCodes.Status200OK)
        where T : notnull
    {
        if (!OrganizationAccessFilter.GetAccess(HttpContext).CanAdmin)
            return this.ToProblem(OcharoEventsErrors.Forbidden("Only the organization's admins set its Telegram delivery."));
        return await call() switch
        {
            Result<T>.Success { Value: var value } => StatusCode(status, ApiResponse<T>.Ok(value)),
            Result<T>.Failure failure => this.ToProblem(failure.Error),
            _ => throw new InvalidOperationException("A result is a success or a failure."),
        };
    }
}
