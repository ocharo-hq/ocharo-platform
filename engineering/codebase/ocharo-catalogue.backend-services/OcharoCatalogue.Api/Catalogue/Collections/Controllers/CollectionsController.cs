using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Application.Catalogue.Collections.Commands;
using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Application.Catalogue.Collections.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoCatalogue.Api.Catalogue.Collections.Controllers;

/// <summary>Exposes product collections over HTTP.</summary>
[ApiController]
[Route("api/catalogue/collections")]
public sealed class CollectionsController(ISender sender) : ControllerBase
{
    /// <summary>Gets all collections.</summary>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await sender.SendAsync(new CollectionGetAllQuery(), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<CollectionDetail>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a collection by id.</summary>
    /// <param name="id">The collection id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken ct)
    {
        var result = await sender.SendAsync(new CollectionGetByIdQuery { Id = id }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CollectionDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Creates a collection.</summary>
    /// <param name="input">The collection fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost]
    public async Task<IActionResult> Create(CollectionInput input, CancellationToken ct)
    {
        var result = await sender.SendAsync(new CollectionCreateCommand { Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CollectionDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Updates a collection.</summary>
    /// <param name="id">The collection id.</param>
    /// <param name="input">The collection fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateById(
        string id,
        CollectionInput input,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new CollectionUpdateCommand { Id = id, Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CollectionDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Deletes a collection.</summary>
    /// <param name="id">The collection id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteById(string id, CancellationToken ct)
    {
        var result = await sender.SendAsync(new CollectionDeleteCommand { Id = id }, ct);

        return result.Match<IActionResult>(
            () => NoContent(),
            fail => this.ToProblem(fail.Error));
    }
}
