using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Api.Catalogue.Settings.Requests;
using OcharoCatalogue.Application.Catalogue.Settings.Models;
using OcharoCatalogue.Application.Catalogue.Settings.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoCatalogue.Api.Catalogue.Settings.Controllers;

/// <summary>Exposes the catalogue settings over HTTP.</summary>
[ApiController]
[Route("api/catalogue/settings")]
public sealed class SettingsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the catalogue settings.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await sender.SendAsync(new SettingsGetQuery(), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CatalogueSettingsDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Updates the catalogue settings.</summary>
    /// <param name="request">The new settings and the revision the caller edited.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPut]
    public async Task<IActionResult> Update(UpdateSettingsApiRequest request, CancellationToken ct)
    {
        var result = await sender.SendAsync(request.ToCommand(), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CatalogueSettingsDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }
}
