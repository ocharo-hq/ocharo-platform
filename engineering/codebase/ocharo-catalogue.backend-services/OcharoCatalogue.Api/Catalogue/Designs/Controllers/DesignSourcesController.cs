using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Designs.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoCatalogue.Api.Catalogue.Designs.Controllers;

/// <summary>Exposes the products and settings the designer composes from over HTTP.</summary>
[ApiController]
[Route("api/catalogue/design-source")]
public sealed class DesignSourcesController(ISender sender) : ControllerBase
{
    /// <summary>Gets every product in catalogue order, and the settings.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await sender.SendAsync(new DesignSourceGetQuery(), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<DesignSource>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }
}
