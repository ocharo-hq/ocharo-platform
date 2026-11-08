using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Api.Access;
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
    Microsoft.Extensions.Options.IOptions<CatalogueAccessOptions> access,
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

    /// <summary>Serves public media to anyone and private media to members only, reading a session only when one is sent.</summary>
    [HttpGet("media/{sha256}")]
    [AllowAnonymous]
    public Task<IActionResult> Media(string sha256, CancellationToken ct)
    {
        return ServeAsync(
            sha256,
            false,
            ct);
    }

    [HttpGet("media/{sha256}/preview")]
    [AllowAnonymous]
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
        // Private bytes answer as missing outside the bound organization, so a caller cannot probe for them.
        if (!isPublic && !CatalogueAccess.IsMember(User, access.Value.OrganizationId))
            return NotFound();
        var opened = await media.OpenAsync(
            sha256,
            preview,
            ct);
        if (opened is null)
            return NotFound();
        // Visibility can change after withdrawal or review; public bytes must be revalidated, and a redirect to a Files
        // link is asked again each time, so this route keeps deciding.
        Response.Headers.CacheControl = isPublic ? "public, no-cache" : "private, no-store";
        if (opened.Location is { } location)
            return Redirect(location.AbsoluteUri);
        Response.Headers.ETag = $"\"{opened.Record.Sha256}{(preview ? "-p" : "")}\"";
        return File(opened.Content!, opened.ContentType);
    }

    public sealed record SettingsInput(CatalogueSettings Settings, int ExpectedRevision);
}
