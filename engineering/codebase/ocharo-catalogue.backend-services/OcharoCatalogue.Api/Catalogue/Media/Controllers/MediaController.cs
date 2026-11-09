using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Media.Models;
using OcharoCatalogue.Application.Catalogue.Media.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;
using WoW.Two.Sdk.Beta.Be.Web.SecureHeaders;

namespace OcharoCatalogue.Api.Catalogue.Media.Controllers;

/// <summary>Exposes stored product images over HTTP.</summary>
/// <remarks>Public media is served to anyone and private media to members only, reading a session only when one is
/// sent; other Ocharo origins (Marketing, Blog) show these images in place, so they may embed them.</remarks>
[ApiController]
[Route("api/catalogue/media")]
public sealed class MediaController(ISender sender) : ControllerBase
{
    /// <summary>Gets an original image by its SHA-256.</summary>
    /// <param name="sha256">The image's SHA-256.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{sha256}")]
    [AllowAnonymous]
    [AllowCrossOriginEmbedding]
    public async Task<IActionResult> GetById(string sha256, CancellationToken ct)
    {
        var result = await sender.SendAsync(new MediaGetQuery { Sha256 = sha256, Preview = false }, ct);
        Response.Headers.CacheControl = "no-store";

        return result.Match<IActionResult>(
            ok => Serve(ok.Data, false),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets an image's web preview by its SHA-256.</summary>
    /// <param name="sha256">The image's SHA-256.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{sha256}/preview")]
    [AllowAnonymous]
    [AllowCrossOriginEmbedding]
    public async Task<IActionResult> GetPreviewById(string sha256, CancellationToken ct)
    {
        var result = await sender.SendAsync(new MediaGetQuery { Sha256 = sha256, Preview = true }, ct);
        Response.Headers.CacheControl = "no-store";

        return result.Match<IActionResult>(
            ok => Serve(ok.Data, true),
            fail => this.ToProblem(fail.Error));
    }

    private IActionResult Serve(MediaDelivery delivery, bool preview)
    {
        // Visibility can change after withdrawal or review; public bytes must be revalidated, and a redirect to a Files
        // link is asked again each time, so this route keeps deciding.
        Response.Headers.CacheControl = delivery.IsPublic ? "public, no-cache" : "private, no-store";
        var opened = delivery.Content;
        if (opened.Location is { } location)
        {
            return Redirect(location.AbsoluteUri);
        }

        Response.Headers.ETag = $"\"{opened.Record.Sha256}{(preview ? "-p" : "")}\"";
        return File(opened.Content!, opened.ContentType);
    }
}
