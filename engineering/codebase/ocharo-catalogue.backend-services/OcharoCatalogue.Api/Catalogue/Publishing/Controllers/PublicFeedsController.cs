using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Api.Catalogue.Publishing.Serializers;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoCatalogue.Api.Catalogue.Publishing.Controllers;

/// <summary>Exposes the public catalogue feed over HTTP.</summary>
/// <remarks>The feed answers its frozen wire shape, unenveloped with null fields written (<see
/// cref="PublicFeedJson"/>):
/// Marketing and Blog read it.</remarks>
[ApiController]
[AllowAnonymous]
[Route("api/public/catalogue")]
public sealed class PublicFeedsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the active products and published collections in one language.</summary>
    /// <param name="locale">The feed's language; Uzbek when omitted.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? locale, CancellationToken ct)
    {
        var result = await sender.SendAsync(new PublicCatalogueGetQuery { Locale = locale }, ct);

        return result.Match<IActionResult>(
            ok => new JsonResult(ok.Data, PublicFeedJson.Options),
            fail => this.ToProblem(fail.Error));
    }
}
