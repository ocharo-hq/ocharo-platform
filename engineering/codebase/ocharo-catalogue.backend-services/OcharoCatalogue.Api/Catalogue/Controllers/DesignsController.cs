using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Services;

namespace OcharoCatalogue.Api.Catalogue.Controllers;

/// <summary>Local catalogue authoring; the existing loopback and origin middleware remains authoritative.</summary>
[ApiController]
[Route("api/catalogue/designs")]
public sealed class DesignsController(CatalogueDesignService designs, CatalogueReleaseService releases) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool archived, CancellationToken ct)
    {
        return Ok(await designs.ListAsync(archived, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CatalogueDesignInput input, CancellationToken ct)
    {
        return Ok(await designs.SaveAsync(
            null,
            input,
            ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        return Ok(await designs.GetAsync(id, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        CatalogueDesignInput input,
        CancellationToken ct)
    {
        return Ok(await designs.SaveAsync(
            id,
            input,
            ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Archive(
        Guid id,
        [FromQuery] int expectedRevision,
        CancellationToken ct)
    {
        await designs.ArchiveAsync(
            id,
            expectedRevision,
            ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(
        Guid id,
        DuplicateInput input,
        CancellationToken ct)
    {
        return Ok(await designs.DuplicateAsync(
            id,
            input.Title,
            ct));
    }

    [HttpGet("{id:guid}/revisions")]
    public async Task<IActionResult> Revisions(Guid id, CancellationToken ct)
    {
        return Ok(await designs.RevisionsAsync(id, ct));
    }

    [HttpGet("{id:guid}/revisions/{revision:int}")]
    public async Task<IActionResult> Revision(
        Guid id,
        int revision,
        CancellationToken ct)
    {
        return Ok(await designs.RevisionAsync(
            id,
            revision,
            ct));
    }

    [HttpGet("{id:guid}/preparation")]
    public async Task<IActionResult> Preparation(Guid id, CancellationToken ct)
    {
        return Ok(await releases.PreparationAsync(id, ct));
    }

    [HttpGet("{id:guid}/releases")]
    public async Task<IActionResult> Releases(Guid id, CancellationToken ct)
    {
        return Ok(await releases.ReleasesAsync(id, ct));
    }

    [HttpPost("{id:guid}/prepare")]
    [RequestSizeLimit(CatalogueReleaseService.MaximumTotalBytes + 2 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = CatalogueReleaseService.MaximumTotalBytes + 2 * 1024 * 1024)]
    public async Task<IActionResult> Prepare(Guid id, CancellationToken ct)
    {
        PrepareInput? input;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            if (form["metadata"].Count != 1 || form["metadata"].ToString().Length > 1000 || form.Files.Count > 201)
                throw new CatalogueException("Catalogue preparation metadata is invalid.");
            try
            {
                input = JsonSerializer.Deserialize<PrepareInput>(form["metadata"].ToString(), CatalogueJson.Options);
            }
            catch (JsonException)
            {
                throw new CatalogueException("Catalogue preparation metadata is invalid.");
            }

            long total = 0;
            foreach (var file in form.Files)
            {
                total += file.Length;
                if (file.Length <= 0
                    || file.Length > 64 * 1024 * 1024
                    || total > CatalogueReleaseService.MaximumTotalBytes
                    || files.ContainsKey(file.FileName))
                    throw new CatalogueException("Catalogue artifacts are oversized or duplicated.");
                await using var stream = file.OpenReadStream();
                using var bytes = new MemoryStream();
                await stream.CopyToAsync(bytes, ct);
                files.Add(file.FileName, bytes.ToArray());
            }
        }
        else
        {
            try
            {
                input = await Request.ReadFromJsonAsync<PrepareInput>(ct);
            }
            catch (JsonException)
            {
                throw new CatalogueException("Catalogue preparation metadata is invalid.");
            }
        }

        if (input is null
            || input.ExpectedRevision < 1
            || input.Fingerprint is not { Length: 64 }
            || !input.Fingerprint.All(char.IsAsciiHexDigit))
            throw new CatalogueException("Get preparation details before rendering this saved catalogue.");
        return Ok(await releases.PrepareAsync(
            id,
            input.ExpectedRevision,
            input.Fingerprint,
            files,
            ct));
    }

    [HttpPost("{id:guid}/releases/{releaseId:guid}/publish")]
    public async Task<IActionResult> Publish(
        Guid id,
        Guid releaseId,
        PublishInput input,
        CancellationToken ct)
    {
        return Ok(await releases.PublishAsync(
            id,
            releaseId,
            input.ExpectedRevision,
            ct));
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(
        Guid id,
        PublishInput input,
        CancellationToken ct)
    {
        await releases.WithdrawAsync(
            id,
            input.ExpectedRevision,
            ct);
        return NoContent();
    }

    public sealed record DuplicateInput(string? Title);
    public sealed record PrepareInput(int ExpectedRevision, string Fingerprint);
    public sealed record PublishInput(int ExpectedRevision);
}
