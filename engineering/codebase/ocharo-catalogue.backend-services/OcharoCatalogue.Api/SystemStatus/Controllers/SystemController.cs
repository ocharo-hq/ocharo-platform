using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OcharoCatalogue.Api.SystemStatus.Controllers;

/// <summary>Exposes system status over HTTP.</summary>
[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    /// <summary>Reports service liveness.</summary>
    [HttpGet("status")]
    [AllowAnonymous]
    public IActionResult Status()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { service = "OcharoCatalogue", status = "live", version = BuildIdentity.Version });
    }
}
