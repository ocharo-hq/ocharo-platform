using Microsoft.AspNetCore.Mvc;
using OcharoBrand.Application.Brand;
using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Api.Brand.Controllers;

/// <summary>Brand editing: identity, logo files per slot, publishing releases, scheduled appearances, consumers.</summary>
[ApiController]
[Route("api/brand")]
public sealed class BrandAdminController(BrandService brand, PublicBrandService published, IMediaStore media) : ControllerBase
{
    [HttpGet("draft")]
    public async Task<IActionResult> Draft(CancellationToken ct) => Ok(await brand.GetAsync(ct));

    [HttpPut("identity")]
    public async Task<IActionResult> SaveIdentity(IdentityInput input, CancellationToken ct) => Ok(await brand.SaveIdentityAsync(input, ct));

    [HttpPost("logos/{slot}")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> Upload(string slot, [FromForm] IFormFile file, [FromForm] string? note, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return Ok(await brand.UploadLogoAsync(slot, buffer.ToArray(), file.FileName, note, ct));
    }

    [HttpPost("logos/{slot}/current")]
    public async Task<IActionResult> SetCurrent(string slot, CurrentLogoInput input, CancellationToken ct) => Ok(await brand.SetCurrentLogoAsync(slot, input, ct));

    [HttpPost("logos/versions/{id}/status")]
    public async Task<IActionResult> SetStatus(string id, StatusInput input, CancellationToken ct) => Ok(await brand.SetLogoStatusAsync(id, input.Status, ct));

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken ct) => Ok(await brand.DraftHistoryAsync(ct));

    [HttpPost("publish")]
    public async Task<IActionResult> Publish(PublishInput input, CancellationToken ct) => Ok(await brand.PublishAsync(input, ct));

    [HttpGet("releases")]
    public async Task<IActionResult> Releases(CancellationToken ct) => Ok(await brand.ReleasesAsync(ct));

    [HttpGet("appearances")]
    public async Task<IActionResult> Appearances(CancellationToken ct) => Ok(await brand.AppearancesAsync(ct));

    [HttpPost("appearances")]
    public async Task<IActionResult> SaveAppearance(AppearanceInput input, CancellationToken ct) => Ok(await brand.SaveAppearanceAsync(input, ct));

    [HttpPost("appearances/{id}/cancel")]
    public async Task<IActionResult> CancelAppearance(string id, CancellationToken ct) => Ok(await brand.CancelAppearanceAsync(id, ct));

    [HttpGet("consumers")]
    public async Task<IActionResult> Consumers(CancellationToken ct) => Ok(await published.ConsumersAsync(ct));

    public sealed record StatusInput(string Status);

    /// <summary>Any stored file, drafts included (loopback admin only; public media serves disclosed files only).</summary>
    [HttpGet("media/{sha256}")]
    public Task<IActionResult> Media(string sha256, CancellationToken ct) => ServeAsync(sha256, false, ct);

    [HttpGet("media/{sha256}/preview")]
    public Task<IActionResult> MediaPreview(string sha256, CancellationToken ct) => ServeAsync(sha256, true, ct);

    private async Task<IActionResult> ServeAsync(string sha256, bool preview, CancellationToken ct)
    {
        var opened = await media.OpenAsync(sha256, preview, ct);
        if (opened is null) return NotFound();
        var (record, content) = opened.Value;
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return File(content, preview && record.PreviewPath is not null ? "image/webp" : record.ContentType);
    }
}
