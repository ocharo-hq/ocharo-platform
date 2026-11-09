using Microsoft.AspNetCore.Mvc;
using OcharoBrand.Api.Brand.Requests;
using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoBrand.Api.Brand.Controllers;

/// <summary>Exposes the brand draft, its logos, releases, appearances and consumers over HTTP.</summary>
[ApiController]
[Route("api/brand")]
public sealed class BrandsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the draft with its logo versions and unpublished changes.</summary>
    [HttpGet("draft")]
    public async Task<IActionResult> Draft(CancellationToken ct)
    {
        return this.ToActionResult(await sender.SendAsync(new DraftGetQuery(), ct));
    }

    /// <summary>Saves the draft's identity at the revision the editor opened.</summary>
    [HttpPut("identity")]
    public async Task<IActionResult> SaveIdentity(SaveIdentityApiRequest request, CancellationToken ct)
    {
        var command = new IdentitySaveCommand { ExpectedRevision = request.ExpectedRevision, Identity = request.Identity };
        return this.ToActionResult(await sender.SendAsync(command, ct));
    }

    /// <summary>Uploads a logo file as a new version of <paramref name="slot"/>.</summary>
    [HttpPost("logos/{slot}")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        string slot,
        [FromForm] IFormFile file,
        [FromForm] string? note,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var command = new LogoUploadCommand { Slot = slot, Bytes = buffer.ToArray(), FileName = file.FileName, Note = note };
        return this.ToActionResult(await sender.SendAsync(command, ct));
    }

    /// <summary>Points <paramref name="slot"/> of the draft at one of its versions.</summary>
    [HttpPost("logos/{slot}/current")]
    public async Task<IActionResult> SetCurrent(
        string slot,
        SetCurrentLogoApiRequest request,
        CancellationToken ct)
    {
        var command = new LogoCurrentSetCommand { Slot = slot, ExpectedRevision = request.ExpectedRevision, VersionId = request.VersionId };
        return this.ToActionResult(await sender.SendAsync(command, ct));
    }

    /// <summary>Marks a logo version draft or approved.</summary>
    [HttpPost("logos/versions/{id}/status")]
    public async Task<IActionResult> SetStatus(
        string id,
        SetLogoStatusApiRequest request,
        CancellationToken ct)
    {
        var command = new LogoStatusSetCommand { VersionId = id, Status = request.Status };
        return this.ToActionResult(await sender.SendAsync(command, ct));
    }

    /// <summary>Gets every saved draft revision, newest first.</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        return this.ToActionResult(await sender.SendAsync(new RevisionGetAllQuery(), ct));
    }

    /// <summary>Publishes the draft as the next release.</summary>
    [HttpPost("publish")]
    public async Task<IActionResult> Publish(PublishReleaseApiRequest request, CancellationToken ct)
    {
        var command = new ReleasePublishCommand { ExpectedRevision = request.ExpectedRevision, Note = request.Note };
        return this.ToActionResult(await sender.SendAsync(command, ct));
    }

    /// <summary>Gets every release, newest first.</summary>
    [HttpGet("releases")]
    public async Task<IActionResult> Releases(CancellationToken ct)
    {
        return this.ToActionResult(await sender.SendAsync(new ReleaseGetAllQuery(), ct));
    }

    /// <summary>Gets every appearance, latest start first.</summary>
    [HttpGet("appearances")]
    public async Task<IActionResult> Appearances(CancellationToken ct)
    {
        return this.ToActionResult(await sender.SendAsync(new AppearanceGetAllQuery(), ct));
    }

    /// <summary>Schedules an appearance, or changes a scheduled one.</summary>
    [HttpPost("appearances")]
    public async Task<IActionResult> SaveAppearance(SaveAppearanceApiRequest request, CancellationToken ct)
    {
        var command = new AppearanceSaveCommand
        {
            Id = request.Id,
            Name = request.Name,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Overrides = request.Overrides,
            AccentHex = request.AccentHex,
            Note = request.Note,
            LinkLabel = request.LinkLabel,
            LinkUrl = request.LinkUrl,
        };
        return this.ToActionResult(await sender.SendAsync(command, ct));
    }

    /// <summary>Cancels a scheduled appearance.</summary>
    [HttpPost("appearances/{id}/cancel")]
    public async Task<IActionResult> CancelAppearance(string id, CancellationToken ct)
    {
        return this.ToActionResult(await sender.SendAsync(new AppearanceCancelCommand { Id = id }, ct));
    }

    /// <summary>Gets every app that read the public brand.</summary>
    [HttpGet("consumers")]
    public async Task<IActionResult> Consumers(CancellationToken ct)
    {
        return this.ToActionResult(await sender.SendAsync(new ConsumerGetAllQuery(), ct));
    }

    /// <summary>Any stored file, drafts included (members only; public media serves disclosed files only).</summary>
    [HttpGet("media/{sha256}")]
    public async Task<IActionResult> Media(string sha256, CancellationToken ct)
    {
        return Serve(await sender.SendAsync(new MediaOpenQuery { Sha256 = sha256 }, ct));
    }

    /// <summary>The WebP preview of any stored file.</summary>
    [HttpGet("media/{sha256}/preview")]
    public async Task<IActionResult> MediaPreview(string sha256, CancellationToken ct)
    {
        return Serve(await sender.SendAsync(new MediaOpenQuery { Sha256 = sha256, Preview = true }, ct));
    }

    /// <summary>Serves a file privately: a Files link redirects for 5 minutes, local bytes stay cached a year.</summary>
    private IActionResult Serve(AppResult<MediaContent> result)
    {
        return result.Match<IActionResult>(
            success =>
            {
                if (success.Data.Location is { } location)
                {
                    Response.Headers.CacheControl = $"private, max-age={MediaContent.RedirectMaxAgeSeconds}";
                    return Redirect(location.AbsoluteUri);
                }
                Response.Headers.CacheControl = "private, max-age=31536000, immutable";
                return File(success.Data.Content!, success.Data.ContentType);
            },
            failure => this.ToProblem(failure.Error));
    }
}
