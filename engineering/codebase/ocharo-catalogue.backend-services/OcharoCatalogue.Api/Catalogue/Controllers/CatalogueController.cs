using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue;

namespace OcharoCatalogue.Api.Catalogue.Controllers;

/// <summary>Settings, media, import and export.</summary>
[ApiController]
[Route("api/catalogue")]
public sealed class CatalogueController(SettingsService settings, CatalogueExportService exports, WorkspaceCatalogueImporter importer,
    IMediaStore media, Microsoft.Extensions.Options.IOptions<CatalogueOptions> options) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var (value, revision) = await settings.GetWithRevisionAsync(ct);
        return Ok(new { settings = value, revision, sourceRoot = options.Value.SourceRoot });
    }

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings(SettingsInput input, CancellationToken ct)
    {
        var (value, revision) = await settings.SaveAsync(input.Settings, input.ExpectedRevision, ct);
        return Ok(new { settings = value, revision, sourceRoot = options.Value.SourceRoot });
    }

    [HttpGet("media/{sha256}")]
    public Task<IActionResult> Media(string sha256, CancellationToken ct) => ServeAsync(sha256, false, ct);

    [HttpGet("media/{sha256}/preview")]
    public Task<IActionResult> Preview(string sha256, CancellationToken ct) => ServeAsync(sha256, true, ct);

    [HttpPost("import/workspace")]
    public async Task<IActionResult> Import(ImportInput input, CancellationToken ct) => Ok(await importer.ImportAsync(input, ct));

    [HttpGet("export/catalogue.json")]
    public async Task<IActionResult> ExportJson(CancellationToken ct) =>
        File(await exports.JsonBundleAsync(ct), "application/json", "ocharo-catalogue.json");

    [HttpGet("export/prices.csv")]
    public async Task<IActionResult> ExportCsv(CancellationToken ct) =>
        File(await exports.CsvAsync(ct), "text/csv; charset=utf-8", "ocharo-prices.csv");

    [HttpGet("export/workspace.zip")]
    public async Task<IActionResult> ExportWorkspace(CancellationToken ct) =>
        File(await exports.WorkspaceSchemaZipAsync(ct), "application/zip", "ocharo-products-schema.zip");

    private async Task<IActionResult> ServeAsync(string sha256, bool preview, CancellationToken ct)
    {
        var opened = await media.OpenAsync(sha256, preview, ct);
        if (opened is null) return NotFound();
        var (record, content) = opened.Value;
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers.ETag = $"\"{record.Sha256}{(preview ? "-p" : "")}\"";
        return File(content, preview && record.PreviewPath is not null ? "image/webp" : record.ContentType);
    }

    public sealed record SettingsInput(CatalogueSettings Settings, int ExpectedRevision);
}
