using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Exporting.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoCatalogue.Api.Catalogue.Exporting.Controllers;

/// <summary>Exposes catalogue exports over HTTP.</summary>
[ApiController]
[Route("api/catalogue/export")]
public sealed class ExportsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the full JSON backup.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("catalogue.json")]
    public async Task<IActionResult> GetJson(CancellationToken ct)
    {
        var result = await sender.SendAsync(new ExportJsonGetQuery(), ct);

        return result.Match<IActionResult>(
            ok => File(
                ok.Data,
                "application/json",
                "ocharo-catalogue.json"),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the CSV price sheet.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("prices.csv")]
    public async Task<IActionResult> GetCsv(CancellationToken ct)
    {
        var result = await sender.SendAsync(new ExportCsvGetQuery(), ct);

        return result.Match<IActionResult>(
            ok => File(
                ok.Data,
                "text/csv; charset=utf-8",
                "ocharo-prices.csv"),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a ZIP of per-product files in the workspace product schema.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("workspace.zip")]
    public async Task<IActionResult> GetWorkspace(CancellationToken ct)
    {
        var result = await sender.SendAsync(new ExportWorkspaceGetQuery(), ct);

        return result.Match<IActionResult>(
            ok => File(
                ok.Data,
                "application/zip",
                "ocharo-products-schema.zip"),
            fail => this.ToProblem(fail.Error));
    }
}
