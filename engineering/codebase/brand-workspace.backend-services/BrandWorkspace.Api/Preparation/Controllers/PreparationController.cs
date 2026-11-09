using BrandWorkspace.Api.Access.Filters;
using BrandWorkspace.Api.Foundation.Filters;
using BrandWorkspace.Api.Preparation.Models;
using BrandWorkspace.Api.Preparation.Readers;
using BrandWorkspace.Application.Preparation.Commands;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Queries;
using BrandWorkspace.Domain.Preparation.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace BrandWorkspace.Api.Preparation.Controllers;

/// <summary>Exposes an organization's brands, preparation images, drafts and manual exports over HTTP.</summary>
[ApiController]
[Route("api/organizations/{organizationId:guid}")]
[Authorize]
[OrganizationAccess]
public sealed class PreparationController(ISender sender) : ControllerBase
{
    /// <summary>Gets the organization's brands.</summary>
    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands(CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new BrandGetAllQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext) },
            cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<IReadOnlyList<Brand>>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Configures a brand.</summary>
    [HttpPost("brands")]
    public async Task<IActionResult> CreateBrand([FromBody] CreateBrandRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new BrandCreateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            Name = request.Name,
            TimeZone = request.TimeZone,
        }, cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<Brand>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the organization's preparation images.</summary>
    [HttpGet("media")]
    public async Task<IActionResult> GetMedia(CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new PreparationMediaGetAllQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext) },
            cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<IReadOnlyList<MediaAsset>>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Stores one image and its provenance, sent as multipart form data.</summary>
    [HttpPost("media")]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        var read = await PreparationUploadReader.ReadAsync(Request, cancellationToken);
        if (read.IsFailure(out var error, out var upload))
            return this.ToProblem(error);
        var result = await sender.SendAsync(new PreparationMediaUploadCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            FileName = upload.FileName,
            Provenance = upload.Provenance,
            Bytes = upload.Bytes,
        }, cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<MediaAsset>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Answers an image's bytes, never a redirect: the design export fetch()es this route into a blob, and Files'
    /// links send no CORS header.</summary>
    [HttpGet("media/{mediaId:guid}/content")]
    public async Task<IActionResult> GetMediaContent(Guid mediaId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new PreparationMediaContentGetQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), MediaId = mediaId },
            cancellationToken);
        return result.Match<IActionResult>(ok => Bytes(ok.Data), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the drafts, optionally of one brand.</summary>
    [HttpGet("drafts")]
    public async Task<IActionResult> GetDrafts([FromQuery] Guid? brandId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DraftGetAllQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), BrandId = brandId }, cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<IReadOnlyList<DraftSummary>>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Creates a draft.</summary>
    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft([FromBody] SaveDraftRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DraftSaveCommand { Access = OrganizationAccessFilter.GetAccess(HttpContext), Input = request.ToInput() },
            cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<Draft>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a draft at its current revision.</summary>
    [HttpGet("drafts/{draftId:guid}")]
    public async Task<IActionResult> GetDraft(Guid draftId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DraftGetQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), DraftId = draftId }, cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<Draft>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Replaces a draft at the revision the operator edited.</summary>
    [HttpPut("drafts/{draftId:guid}")]
    public async Task<IActionResult> SaveDraft(Guid draftId, [FromBody] SaveDraftRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new DraftSaveCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            DraftId = draftId,
            ExpectedRevision = request.ExpectedRevision,
            Input = request.ToInput(),
        }, cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<Draft>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Requests review of the current revision.</summary>
    [HttpPost("drafts/{draftId:guid}/request-review")]
    public Task<IActionResult> RequestReview(Guid draftId, [FromBody] RevisionRequest request, CancellationToken cancellationToken)
    {
        return ReviewAsync(draftId, request.ExpectedRevision, ReviewAction.Request, null, cancellationToken);
    }

    /// <summary>Approves the current revision.</summary>
    [HttpPost("drafts/{draftId:guid}/approve")]
    public Task<IActionResult> Approve(Guid draftId, [FromBody] RevisionRequest request, CancellationToken cancellationToken)
    {
        return ReviewAsync(draftId, request.ExpectedRevision, ReviewAction.Approve, null, cancellationToken);
    }

    /// <summary>Revokes the current revision's approval, with a reason.</summary>
    [HttpPost("drafts/{draftId:guid}/revoke-approval")]
    public Task<IActionResult> RevokeApproval(Guid draftId, [FromBody] RevisionRequest request, CancellationToken cancellationToken)
    {
        return ReviewAsync(draftId, request.ExpectedRevision, ReviewAction.Revoke, request.Reason, cancellationToken);
    }

    /// <summary>Prepares an approved revision's manual package once per retry identity.</summary>
    [HttpPost("drafts/{draftId:guid}/exports")]
    public async Task<IActionResult> CreateExport(Guid draftId, [FromBody] CreateExportRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new ExportCreateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            DraftId = draftId,
            ExpectedRevision = request.ExpectedRevision,
            IdempotencyKey = request.IdempotencyKey,
        }, cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<ManualExport>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Downloads an export: the desk links here with a plain link, so a redirect to the private Files file, saved
    /// under the same name, serves it; membership still decides, and the answer stays no-store.</summary>
    [HttpGet("exports/{exportId:guid}/download")]
    public async Task<IActionResult> DownloadExport(Guid exportId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ExportOpenQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), ExportId = exportId }, cancellationToken);
        return result.Match<IActionResult>(
            ok =>
            {
                Response.Headers.XContentTypeOptions = "nosniff";
                return ok.Data.Location is { } location
                    ? Redirect(location.AbsoluteUri)
                    : File(ok.Data.Bytes!, ok.Data.ContentType, $"manual-content-{exportId:D}.zip");
            },
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Applies a review transition at the revision it names.</summary>
    private async Task<IActionResult> ReviewAsync(Guid draftId, int expectedRevision, ReviewAction action, string? reason,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new DraftReviewCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            DraftId = draftId,
            ExpectedRevision = expectedRevision,
            Action = action,
            Reason = reason,
        }, cancellationToken);
        return result.Match<IActionResult>(ok => Ok(ApiResponse<Draft>.Ok(ok.Data)), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Answers an image's bytes under its own content type.</summary>
    private FileContentResult Bytes(MediaContent media)
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(media.Bytes, media.Asset.ContentType);
    }
}
