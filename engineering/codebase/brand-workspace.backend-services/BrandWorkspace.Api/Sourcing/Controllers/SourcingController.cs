using BrandWorkspace.Api.Access.Filters;
using BrandWorkspace.Api.Foundation.Filters;
using BrandWorkspace.Api.Foundation.Models;
using BrandWorkspace.Api.Sourcing.Models;
using BrandWorkspace.Api.Sourcing.Readers;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Application.Sourcing.Queries;
using BrandWorkspace.Domain.Sourcing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace BrandWorkspace.Api.Sourcing.Controllers;

/// <summary>
/// Exposes an organization's suppliers, their people and routes, and shared supplier media over HTTP.
/// </summary>
[ApiController]
[Route("api/organizations/{organizationId:guid}/sourcing")]
[Authorize]
[OrganizationAccess]
public sealed class SourcingController(ISender sender) : ControllerBase
{
    /// <summary>Gets the suppliers matching the search, a page at a time.</summary>
    [HttpGet("suppliers")]
    public async Task<IActionResult> GetSuppliers(
        [FromQuery] string? query,
        [FromQuery] string? stage,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new SupplierGetAllQuery
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            Query = query,
            Stage = stage,
            Limit = limit ?? 50,
            Offset = offset ?? 0,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<SupplierSummary>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Records a supplier.</summary>
    [HttpPost("suppliers")]
    public async Task<IActionResult> CreateSupplier(
        [FromBody] SupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new SupplierCreateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            Name = request.Name,
            Stage = request.Stage,
            Country = request.Country,
            City = request.City,
            Website = request.Website,
            Notes = request.Notes,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<Supplier>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets one supplier with its people and routes.</summary>
    [HttpGet("suppliers/{supplierId:guid}")]
    public async Task<IActionResult> GetSupplier(Guid supplierId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new SupplierGetQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), SupplierId = supplierId },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<Supplier>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Replaces a supplier at the revision the operator edited.</summary>
    [HttpPut("suppliers/{supplierId:guid}")]
    public async Task<IActionResult> UpdateSupplier(Guid supplierId, [FromBody] UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new SupplierUpdateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            SupplierId = supplierId,
            ExpectedRevision = request.ExpectedRevision,
            Name = request.Name,
            Stage = request.Stage,
            Country = request.Country,
            City = request.City,
            Website = request.Website,
            Notes = request.Notes,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<Supplier>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Records a person who speaks for a supplier.</summary>
    [HttpPost("suppliers/{supplierId:guid}/people")]
    public async Task<IActionResult> CreatePerson(
        Guid supplierId,
        [FromBody] PersonRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new PersonCreateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            SupplierId = supplierId,
            Name = request.Name,
            Role = request.Role,
            Notes = request.Notes,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<SupplierPerson>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Replaces a person at the revision the operator edited.</summary>
    [HttpPut("people/{personId:guid}")]
    public async Task<IActionResult> UpdatePerson(Guid personId, [FromBody] UpdatePersonRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new PersonUpdateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            PersonId = personId,
            ExpectedRevision = request.ExpectedRevision,
            Name = request.Name,
            Role = request.Role,
            Notes = request.Notes,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<SupplierPerson>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Deletes a person at the revision the operator saw.</summary>
    [HttpDelete("people/{personId:guid}")]
    public async Task<IActionResult> DeletePerson(
        Guid personId,
        [FromQuery] int expectedRevision,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new PersonDeleteCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            PersonId = personId,
            ExpectedRevision = expectedRevision,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<RecordDeletedDto>.Ok(new RecordDeletedDto(ok.Data))),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Records a route that reaches a supplier.</summary>
    [HttpPost("suppliers/{supplierId:guid}/routes")]
    public async Task<IActionResult> CreateRoute(
        Guid supplierId,
        [FromBody] RouteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new RouteCreateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            SupplierId = supplierId,
            Channel = request.Channel,
            Destination = request.Destination,
            Label = request.Label,
            SourceUrl = request.SourceUrl,
            Verified = request.Verified,
            IsPrimary = request.IsPrimary,
            PersonId = request.PersonId,
            Notes = request.Notes,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ContactRoute>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Replaces a route at the revision the operator edited.</summary>
    [HttpPut("routes/{routeId:guid}")]
    public async Task<IActionResult> UpdateRoute(
        Guid routeId,
        [FromBody] UpdateRouteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new RouteUpdateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            RouteId = routeId,
            ExpectedRevision = request.ExpectedRevision,
            Channel = request.Channel,
            Destination = request.Destination,
            Label = request.Label,
            SourceUrl = request.SourceUrl,
            Verified = request.Verified,
            IsPrimary = request.IsPrimary,
            PersonId = request.PersonId,
            Notes = request.Notes,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<ContactRoute>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Deletes a route at the revision the operator saw.</summary>
    [HttpDelete("routes/{routeId:guid}")]
    public async Task<IActionResult> DeleteRoute(
        Guid routeId,
        [FromQuery] int expectedRevision,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new RouteDeleteCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            RouteId = routeId,
            ExpectedRevision = expectedRevision,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<RecordDeletedDto>.Ok(new RecordDeletedDto(ok.Data))),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a supplier's media or the inbox, a page at a time.</summary>
    [HttpGet("media")]
    public async Task<IActionResult> GetMedia(
        [FromQuery] Guid? supplierId,
        [FromQuery] bool? unassigned,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new SourcingMediaGetAllQuery
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            SupplierId = supplierId,
            Unassigned = unassigned ?? false,
            Limit = limit ?? 100,
            Offset = offset ?? 0,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<SourcingMedia>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets the media totals for the inbox badge.</summary>
    [HttpGet("media/summary")]
    public async Task<IActionResult> GetMediaSummary(CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new SourcingMediaTotalsGetQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext) },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<SourcingMediaTotals>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Stores one supplier file, sent as multipart form data.</summary>
    [HttpPost("media")]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        var read = await SourcingUploadReader.ReadAsync(Request, cancellationToken);
        if (read.IsFailure(out var error, out var upload))
            return this.ToProblem(error);
        var result = await sender.SendAsync(new SourcingMediaUploadCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            FileName = upload.FileName,
            Bytes = upload.Bytes,
            SupplierId = upload.SupplierId,
            Capture = upload.Capture,
            Caption = upload.Caption,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<SourcingMediaUpload>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Moves media to a supplier, or back to the inbox.</summary>
    [HttpPost("media/assign")]
    public async Task<IActionResult> AssignMedia(
        [FromBody] AssignMediaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new SourcingMediaAssignCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            MediaIds = request.MediaIds,
            SupplierId = request.SupplierId,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<MediaAssignedDto>.Ok(new MediaAssignedDto(ok.Data))),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Replaces a media item's supplier, capture and caption at the revision the operator edited.</summary>
    [HttpPut("media/{mediaId:guid}")]
    public async Task<IActionResult> UpdateMedia(
        Guid mediaId,
        [FromBody] UpdateMediaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new SourcingMediaUpdateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            MediaId = mediaId,
            ExpectedRevision = request.ExpectedRevision,
            SupplierId = request.SupplierId,
            Capture = request.Capture,
            Caption = request.Caption,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<SourcingMedia>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Deletes a media item at the revision the operator saw.</summary>
    [HttpDelete("media/{mediaId:guid}")]
    public async Task<IActionResult> DeleteMedia(
        Guid mediaId,
        [FromQuery] int expectedRevision,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new SourcingMediaDeleteCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            MediaId = mediaId,
            ExpectedRevision = expectedRevision,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<RecordDeletedDto>.Ok(new RecordDeletedDto(ok.Data))),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>
    /// Opens a media item's original: a redirect to a WoW2 Files link once Files holds it, else its bytes; the desk
    /// loads it with img, video and plain links, membership still decides, and the answer stays no-store.
    /// </summary>
    [HttpGet("media/{mediaId:guid}/content")]
    public async Task<IActionResult> OpenMedia(Guid mediaId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new SourcingMediaOpenQuery { Access = OrganizationAccessFilter.GetAccess(HttpContext), MediaId = mediaId },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Stored(ok.Data, enableRangeProcessing: true),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>
    /// Opens a media item's thumbnail: a redirect to the Files variant once Files holds the original, else its bytes.
    /// </summary>
    [HttpGet("media/{mediaId:guid}/thumbnail")]
    public async Task<IActionResult> OpenThumbnail(Guid mediaId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new SourcingThumbnailOpenQuery
            {
                Access = OrganizationAccessFilter.GetAccess(HttpContext),
                MediaId = mediaId,
            },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Stored(ok.Data, enableRangeProcessing: false),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Answers stored content: a redirect to its Files link, else its bytes.</summary>
    private IActionResult Stored(StoredContent content, bool enableRangeProcessing)
    {
        Response.Headers.XContentTypeOptions = "nosniff";
        return content.Location is { } location
            ? Redirect(location.AbsoluteUri)
            : File(content.Bytes!, content.ContentType, enableRangeProcessing);
    }
}
