using System.Globalization;
using BrandWorkspace.Api.Foundation.Filters;
using BrandWorkspace.Api.MediaProcessing.Constants;
using BrandWorkspace.Api.MediaProcessing.Readers;
using BrandWorkspace.Application.MediaProcessing.Commands;
using BrandWorkspace.Application.MediaProcessing.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace BrandWorkspace.Api.MediaProcessing.Controllers;

/// <summary>Exposes private background removal for service callers over HTTP.</summary>
/// <remarks>
/// Only an Identity service token holding <see cref="MediaProcessingPolicy.Scope"/> opens it: none or a bad one answers
/// 401, a missing scope 403; Desk's sign-in cookie never does.
/// </remarks>
[ApiController]
[Route("api/integrations/media/background-removal")]
[Authorize(Policy = MediaProcessingPolicy.Name)]
[NoStore]
public sealed class BackgroundRemovalController(ISender sender) : ControllerBase
{
    /// <summary>Gets whether the image worker removes backgrounds, and its model.</summary>
    [HttpGet("capabilities")]
    public async Task<IActionResult> GetCapabilities(CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new BackgroundRemovalCapabilitiesGetQuery(), cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ok.Data), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Removes the background of the PNG, JPEG or WebP body and answers the PNG.</summary>
    [HttpPost]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Remove(CancellationToken cancellationToken)
    {
        if (ImageBodyReader.Check(Request) is Result.Failure refused)
            return this.ToProblem(refused.Error);
        var read = await ImageBodyReader.ReadAsync(Request, cancellationToken);
        if (read.IsFailure(out var error, out var bytes))
            return this.ToProblem(error);
        var result = await sender.SendAsync(
            new BackgroundRemovalCommand { Bytes = bytes, ContentType = Request.ContentType! },
            cancellationToken);
        return result.Match<IActionResult>(
            ok =>
            {
                Response.Headers.XContentTypeOptions = "nosniff";
                Response.Headers["X-Image-Model"] = ok.Data.Model;
                Response.Headers["X-Image-Width"] = ok.Data.Width.ToString(CultureInfo.InvariantCulture);
                Response.Headers["X-Image-Height"] = ok.Data.Height.ToString(CultureInfo.InvariantCulture);
                return File(ok.Data.Content, "image/png");
            },
            fail => this.ToProblem(fail.Error));
    }
}
