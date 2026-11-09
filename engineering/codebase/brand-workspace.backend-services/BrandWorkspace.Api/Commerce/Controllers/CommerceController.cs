using BrandWorkspace.Api.Access.Filters;
using BrandWorkspace.Api.Commerce.Models;
using BrandWorkspace.Api.Commerce.Readers;
using BrandWorkspace.Api.Foundation.Filters;
using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Domain.Commerce.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace BrandWorkspace.Api.Commerce.Controllers;

/// <summary>
/// Exposes a brand's commerce draft, its media, catalogue editions, releases and creative imports over HTTP.
/// </summary>
[ApiController]
[Route("api/organizations/{organizationId:guid}")]
[Authorize]
[OrganizationAccess]
public sealed class CommerceController(ISender sender) : ControllerBase
{
    /// <summary>Gets the brand's commerce draft; <c>data</c> is empty before its first save.</summary>
    [HttpGet("brands/{brandId:guid}/commerce")]
    public async Task<IActionResult> Get(Guid brandId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new CommerceGetQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), BrandId = brandId },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CommerceDraft?>.Ok(ok.Data.Draft)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Saves the brand's commerce draft at the revision the operator saw.</summary>
    [HttpPut("brands/{brandId:guid}/commerce")]
    public async Task<IActionResult> Save(
        Guid brandId,
        [FromBody] CommerceSaveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new CommerceSaveCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            BrandId = brandId,
            ExpectedRevision = request.ExpectedRevision,
            Snapshot = request.Snapshot,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CommerceDraft>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Adopts a preserved brand into the brand's empty draft.</summary>
    [HttpPost("brands/{brandId:guid}/commerce/adopt-preserved")]
    public async Task<IActionResult> Adopt(
        Guid brandId,
        [FromBody] AdoptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new CommerceAdoptCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            BrandId = brandId,
            Slug = request.Slug,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CommerceDraft>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>
    /// Takes a preparation image into the brand; with WoW2 Files the brand references its file instead of copying its
    /// bytes.
    /// </summary>
    [HttpPost("brands/{brandId:guid}/commerce/media")]
    public async Task<IActionResult> AttachMedia(
        Guid brandId,
        [FromBody] AttachMediaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new CommerceMediaAttachCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            BrandId = brandId,
            ExpectedRevision = request.ExpectedRevision,
            MediaId = request.MediaId,
            Provenance = request.Provenance,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CommerceDraft>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>
    /// Answers the brand's media bytes, never a redirect: the design export fetch()es this route into a blob, and
    /// Files' links send no CORS header.
    /// </summary>
    [HttpGet("brands/{brandId:guid}/commerce/media/{sha256}")]
    public async Task<IActionResult> ReadMedia(Guid brandId, string sha256, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        var result = await sender.SendAsync(new CommerceMediaReadQuery
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            BrandId = brandId,
            Sha256 = sha256,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => File(ok.Data.Bytes!, ok.Data.ContentType),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Attaches a catalogue edition from its artifacts, sent as multipart form data.</summary>
    [HttpPost("brands/{brandId:guid}/commerce/catalogue-editions")]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> AttachEdition(Guid brandId, CancellationToken cancellationToken)
    {
        var read = await CatalogueEditionFormReader.ReadAsync(Request, cancellationToken);
        if (read.IsFailure(out var error, out var form))
            return this.ToProblem(error);
        var result = await sender.SendAsync(new CatalogueEditionAttachCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            BrandId = brandId,
            ExpectedRevision = form.Metadata.ExpectedRevision,
            SourceDesignId = form.Metadata.SourceDesignId,
            SourceRevision = form.Metadata.SourceRevision,
            Files = form.Files,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CommerceDraft>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Publishes one catalogue edition.</summary>
    [HttpPost("brands/{brandId:guid}/commerce/catalogue-editions/{editionId:guid}/publish")]
    public async Task<IActionResult> PublishEdition(Guid brandId, Guid editionId, [FromBody] ReleaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new CatalogueEditionPublishCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            BrandId = brandId,
            ExpectedRevision = request.ExpectedRevision,
            EditionId = editionId,
            Note = request.Note,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CommerceRelease>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Publishes a language's catalogue edition.</summary>
    [HttpPost("brands/{brandId:guid}/commerce/catalogue-editions/{locale}/publish")]
    public async Task<IActionResult> PublishLocaleEdition(
        Guid brandId,
        string locale,
        [FromBody] ReleaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new CatalogueEditionLocalePublishCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            BrandId = brandId,
            ExpectedRevision = request.ExpectedRevision,
            Locale = locale,
            Note = request.Note,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CommerceRelease>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the brand's releases.</summary>
    [HttpGet("brands/{brandId:guid}/commerce/releases")]
    public async Task<IActionResult> GetReleases(Guid brandId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ReleaseGetAllQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), BrandId = brandId },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<CommerceRelease>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Publishes the brand's draft at the revision the owner saw.</summary>
    [HttpPost("brands/{brandId:guid}/commerce/releases")]
    public async Task<IActionResult> Publish(
        Guid brandId,
        [FromBody] ReleaseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new ReleasePublishCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            BrandId = brandId,
            ExpectedRevision = request.ExpectedRevision,
            Note = request.Note,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CommerceRelease>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Withdraws a release, with a reason.</summary>
    [HttpPost("brands/{brandId:guid}/commerce/releases/{releaseId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid brandId, Guid releaseId, [FromBody] WithdrawRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new ReleaseWithdrawCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            BrandId = brandId,
            ReleaseId = releaseId,
            Reason = request.Reason,
        }, cancellationToken);
        return result.Match<IActionResult>(() => NoContent(), fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the brand's creative imports.</summary>
    [HttpGet("brands/{brandId:guid}/creative-imports")]
    public async Task<IActionResult> GetCreativeImports(Guid brandId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new CreativeImportGetAllQuery
            {
                Access = OrganizationAccessFilter.GetAccess(HttpContext),
                BrandId = brandId,
            },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<CreativeImport>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Records a Studio derivative as a creative import.</summary>
    [HttpPost("creative-imports")]
    public async Task<IActionResult> ImportCreative(
        [FromBody] CreativeImportInput request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new CreativeImportCreateCommand
            {
                Access = OrganizationAccessFilter.GetAccess(HttpContext),
                Input = request,
            },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<CreativeImport>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }
}
