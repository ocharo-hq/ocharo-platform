using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Importing.Commands;
using OcharoCatalogue.Application.Catalogue.Importing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoCatalogue.Api.Catalogue.Importing.Controllers;

/// <summary>Exposes workspace imports over HTTP.</summary>
[ApiController]
[Route("api/catalogue/import")]
public sealed class ImportsController(ISender sender) : ControllerBase
{
    /// <summary>Imports the workspace product records with their images.</summary>
    /// <param name="input">The import request.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("workspace")]
    public async Task<IActionResult> CreateWorkspace(ImportInput input, CancellationToken ct)
    {
        var result = await sender.SendAsync(new WorkspaceImportCommand { Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ImportReport>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }
}
