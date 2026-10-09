using Microsoft.AspNetCore.Mvc;
using Ocharo.Sso;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue;

namespace OcharoCatalogue.Api.Catalogue.Controllers;

/// <summary>Settings, media, import and export.</summary>
[ApiController]
[Route("api/catalogue")]
public sealed class CatalogueController(
    SettingsService settings,
    CatalogueExportService exports,
    WorkspaceCatalogueImporter importer,
    IMediaStore media,
    CatalogueMediaAccessService mediaAccess,
    SsoOptions sso,
    Microsoft.Extensions.Options.IOptions<CatalogueOptions> options) : ControllerBase
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
        var (value, revision) = await settings.SaveAsync(
            input.Settings,
            input.ExpectedRevision,
            ct);
        return Ok(new { settings = value, revision, sourceRoot = options.Value.SourceRoot });
    }

    [HttpGet("media/{sha256}")]
    public Task<IActionResult> Media(string sha256, CancellationToken ct)
    {
        return ServeAsync(
            sha256,
            false,
            ct);
    }

    [HttpGet("media/{sha256}/preview")]
    public Task<IActionResult> Preview(string sha256, CancellationToken ct)
    {
        return ServeAsync(
            sha256,
            true,
            ct);
    }

    [HttpPost("import/workspace")]
    public async Task<IActionResult> Import(ImportInput input, CancellationToken ct)
    {
        return Ok(await importer.ImportAsync(input, ct));
    }

    [HttpGet("export/catalogue.json")]
    public async Task<IActionResult> ExportJson(CancellationToken ct)
    {
        return File(
            await exports.JsonBundleAsync(ct),
            "application/json",
            "ocharo-catalogue.json");
    }

    [HttpGet("export/prices.csv")]
    public async Task<IActionResult> ExportCsv(CancellationToken ct)
    {
        return File(
            await exports.CsvAsync(ct),
            "text/csv; charset=utf-8",
            "ocharo-prices.csv");
    }

    [HttpGet("export/workspace.zip")]
    public async Task<IActionResult> ExportWorkspace(CancellationToken ct)
    {
        return File(
            await exports.WorkspaceSchemaZipAsync(ct),
            "application/zip",
            "ocharo-products-schema.zip");
    }

    private async Task<IActionResult> ServeAsync(
        string sha256,
        bool preview,
        CancellationToken ct)
    {
        var isPublic = await mediaAccess.IsPublicAsync(sha256, ct);
        Response.Headers.CacheControl = "no-store";
        if (!isPublic && sso.Enabled && User.Identity?.AuthenticationType != SsoContext.AuthenticationType)
            return NotFound();
        var opened = await media.OpenAsync(
            sha256,
            preview,
            ct);
        if (opened is null)
            return NotFound();
        var (record, content) = opened.Value;
        // Visibility can change after withdrawal or review; public bytes must be revalidated.
        Response.Headers.CacheControl = isPublic ? "public, no-cache" : "private, no-store";
        Response.Headers.ETag = $"\"{record.Sha256}{(preview ? "-p" : "")}\"";
        return File(content, preview && record.PreviewPath is not null ? "image/webp" : record.ContentType);
    }

    public sealed record SettingsInput(CatalogueSettings Settings, int ExpectedRevision);
}
