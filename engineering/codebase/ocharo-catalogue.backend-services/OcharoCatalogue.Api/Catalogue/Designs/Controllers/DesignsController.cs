using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Api.Catalogue.Designs.Requests;
using OcharoCatalogue.Application.Catalogue.Designs.Commands;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Designs.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoCatalogue.Api.Catalogue.Designs.Controllers;

/// <summary>Exposes catalogue designs over HTTP.</summary>
[ApiController]
[Route("api/catalogue/designs")]
public sealed class DesignsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the catalogue designs for the gallery.</summary>
    /// <param name="archived">Whether archived designs are listed too.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool archived, CancellationToken ct)
    {
        var result = await sender.SendAsync(new DesignGetAllQuery { Archived = archived }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<CatalogueDesignSummary>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Creates a catalogue design.</summary>
    /// <param name="input">The design fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost]
    public async Task<IActionResult> Create(CatalogueDesignInput input, CancellationToken ct)
    {
        var result = await sender.SendAsync(new DesignCreateCommand { Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CatalogueDesignDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a catalogue design by id.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await sender.SendAsync(new DesignGetByIdQuery { Id = id }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CatalogueDesignDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Updates a catalogue design.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="input">The design fields.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateById(
        Guid id,
        CatalogueDesignInput input,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new DesignUpdateCommand { Id = id, Input = input }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CatalogueDesignDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Archives a catalogue design.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="expectedRevision">The revision the caller archives.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteById(
        Guid id,
        [FromQuery] int expectedRevision,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(
            new DesignArchiveCommand { Id = id, ExpectedRevision = expectedRevision },
            ct);

        return result.Match<IActionResult>(
            () => NoContent(),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Copies a catalogue design as an inactive design with a fresh slug.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="request">The copy's title.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(
        Guid id,
        DuplicateDesignApiRequest request,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(request.ToCommand(id), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CatalogueDesignDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a design's saved revisions.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{id:guid}/revisions")]
    public async Task<IActionResult> GetRevisions(Guid id, CancellationToken ct)
    {
        var result = await sender.SendAsync(new DesignRevisionGetAllQuery { Id = id }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<DesignRevisionSummary>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a design as it was at one saved revision.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="revision">The revision number.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{id:guid}/revisions/{revision:int}")]
    public async Task<IActionResult> GetRevisionById(
        Guid id,
        int revision,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(new DesignRevisionGetByIdQuery { Id = id, Revision = revision }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CatalogueDesignDetail>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }
}
