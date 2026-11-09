using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OcharoCatalogue.Api.Access;
using OcharoCatalogue.Api.Catalogue.Publishing.Extensions;
using OcharoCatalogue.Api.Catalogue.Publishing.Requests;
using OcharoCatalogue.Application.Catalogue.Publishing.Commands;
using OcharoCatalogue.Application.Catalogue.Publishing.Constants;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoCatalogue.Api.Catalogue.Publishing.Controllers;

/// <summary>Exposes the releases of catalogue designs over HTTP; publishing and withdrawing also need
/// <see cref="CatalogueAccess.ApprovePermission"/>.</summary>
[ApiController]
[Route("api/catalogue/designs")]
public sealed class DesignReleasesController(ISender sender) : ControllerBase
{
    private const long MaximumUploadBytes = ReleaseLimits.MaximumTotalBytes + 2 * 1024 * 1024;

    /// <summary>Gets what a release of the design would hold if prepared now.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{id:guid}/preparation")]
    public async Task<IActionResult> GetPreparation(Guid id, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ReleasePreparationGetQuery { DesignId = id }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ReleasePreparation>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the design's prepared releases.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpGet("{id:guid}/releases")]
    public async Task<IActionResult> GetReleases(Guid id, CancellationToken ct)
    {
        var result = await sender.SendAsync(new ReleaseGetAllQuery { DesignId = id }, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<CatalogueReleaseSummary>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Prepares a release with no rendered artifacts, such as a web-only one.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="request">The design revision and fingerprint the caller rendered against.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("{id:guid}/prepare")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaximumUploadBytes)]
    public async Task<IActionResult> Prepare(
        Guid id,
        PrepareReleaseApiRequest request,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(request.ToCommand(id), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CataloguePreparedRelease>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>
    /// Prepares a release from its rendered PDF and page artifacts, with its metadata as a form field.
    /// </summary>
    /// <param name="id">The design id.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("{id:guid}/prepare")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumUploadBytes)]
    public async Task<IActionResult> PrepareWithArtifacts(Guid id, CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var command = new ReleasePrepareCommand
        {
            DesignId = id,
            FormMetadata = form["metadata"].Select(value => value ?? "").ToArray(),
            Files = await form.Files.ReadUploadsAsync(ct),
        };
        var result = await sender.SendAsync(command, ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CataloguePreparedRelease>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Publishes a prepared release.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="releaseId">The release id.</param>
    /// <param name="request">The design revision the caller publishes.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("{id:guid}/releases/{releaseId:guid}/publish")]
    [Authorize(Policy = CatalogueAccess.ApprovePolicy)]
    public async Task<IActionResult> Publish(
        Guid id,
        Guid releaseId,
        PublishReleaseApiRequest request,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(request.ToCommand(id, releaseId), ct);

        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CatalogueReleaseSummary>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Withdraws every published release of the design.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="request">The design revision the caller withdraws.</param>
    /// <param name="ct">The token that cancels the request.</param>
    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Policy = CatalogueAccess.ApprovePolicy)]
    public async Task<IActionResult> Withdraw(
        Guid id,
        WithdrawReleaseApiRequest request,
        CancellationToken ct)
    {
        var result = await sender.SendAsync(request.ToCommand(id), ct);

        return result.Match<IActionResult>(
            () => NoContent(),
            fail => this.ToProblem(fail.Error));
    }
}
