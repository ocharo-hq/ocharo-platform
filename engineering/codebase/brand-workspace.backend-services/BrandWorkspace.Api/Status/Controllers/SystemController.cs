using BrandWorkspace.Api.Foundation;
using BrandWorkspace.Application.Foundation.Repositories;
using BrandWorkspace.Domain.Foundation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace BrandWorkspace.Api.Status.Controllers;

/// <summary>Exposes system status over HTTP.</summary>
[ApiController]
[Route("api/system")]
public sealed class SystemController(IFoundationRepository repository, TimeProvider time) : ControllerBase
{
    /// <summary>Holds the name the service reports.</summary>
    private const string ServiceName = "brand-workspace";

    /// <summary>Reports service liveness.</summary>
    /// <param name="cancellationToken">Token to cancel the reads.</param>
    /// <returns>The service, its persistence, the worker's last heartbeat and the running informational version; a 503
    /// problem while the database or its schema is not ready.</returns>
    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        try
        {
            if (!await repository.IsReadyAsync(cancellationToken))
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: SchemaReadinessHealthCheck.NotReady);
            var worker = WorkerSnapshot.From(
                await repository.GetLastHeartbeatAsync(cancellationToken),
                time.GetUtcNow());
            return Ok(new
            {
                service = ServiceName,
                stage = "foundation",
                persistence = "PostgreSQL",
                worker,
                version = BuildIdentity.Version,
            });
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: SchemaReadinessHealthCheck.NotReady);
        }
    }
}
