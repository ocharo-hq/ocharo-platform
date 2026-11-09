using BrandWorkspace.Api.Access.Filters;
using BrandWorkspace.Api.DesignDocuments.Models;
using BrandWorkspace.Api.DesignDocuments.Readers;
using BrandWorkspace.Api.Foundation.Filters;
using BrandWorkspace.Application.DesignDocuments.Commands;
using BrandWorkspace.Application.DesignDocuments.Queries;
using BrandWorkspace.Domain.DesignDocuments.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace BrandWorkspace.Api.DesignDocuments.Controllers;

/// <summary>Exposes an organization's editor documents over HTTP.</summary>
[ApiController]
[Route("api/organizations/{organizationId:guid}/design-documents")]
[Authorize]
[OrganizationAccess]
public sealed class DesignDocumentsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the documents of a kind, a page at a time, without their snapshots.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? kind,
        [FromQuery] bool? includeArchived,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new DesignDocumentGetAllQuery
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            Kind = kind,
            IncludeArchived = includeArchived ?? false,
            Limit = limit ?? 50,
            Offset = offset ?? 0,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<DesignDocumentSummary>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Creates a document from a strict JSON body.</summary>
    [HttpPost]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var read = await DesignDocumentRequestReader.ReadAsync<CreateDesignDocumentRequest>(Request, cancellationToken);
        if (read.IsFailure(out var error, out var request))
            return this.ToProblem(error);
        var result = await sender.SendAsync(new DesignDocumentCreateCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            Kind = request.Kind,
            Input = request.ToInput(),
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<DesignDocument>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a document at its current revision.</summary>
    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> Get(Guid documentId, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DesignDocumentGetQuery
            {
                Access = OrganizationAccessFilter.GetAccess(HttpContext),
                DocumentId = documentId,
            },
            cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<DesignDocument>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Replaces a document's content at the revision the editor opened, from a strict JSON body.</summary>
    [HttpPut("{documentId:guid}")]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Save(Guid documentId, CancellationToken cancellationToken)
    {
        var read = await DesignDocumentRequestReader.ReadAsync<SaveDesignDocumentRequest>(Request, cancellationToken);
        if (read.IsFailure(out var error, out var request))
            return this.ToProblem(error);
        var result = await sender.SendAsync(new DesignDocumentSaveCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            DocumentId = documentId,
            ExpectedRevision = request.ExpectedRevision,
            Input = request.ToInput(),
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<DesignDocument>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Archives a document at the revision the editor opened, from a strict JSON body.</summary>
    [HttpPost("{documentId:guid}/archive")]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> Archive(Guid documentId, CancellationToken cancellationToken)
    {
        var read = await DesignDocumentRequestReader.ReadAsync<ArchiveDesignDocumentRequest>(
            Request,
            cancellationToken);
        if (read.IsFailure(out var error, out var request))
            return this.ToProblem(error);
        var result = await sender.SendAsync(new DesignDocumentArchiveCommand
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            DocumentId = documentId,
            ExpectedRevision = request.ExpectedRevision,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<DesignDocument>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a document's saved revisions, a page at a time.</summary>
    [HttpGet("{documentId:guid}/revisions")]
    public async Task<IActionResult> GetRevisions(Guid documentId, [FromQuery] int? limit, [FromQuery] int? offset,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new DesignDocumentRevisionGetAllQuery
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            DocumentId = documentId,
            Limit = limit ?? 50,
            Offset = offset ?? 0,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<DesignDocumentRevision>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }

    /// <summary>Gets a document at one saved revision.</summary>
    [HttpGet("{documentId:guid}/revisions/{revision:int}")]
    public async Task<IActionResult> GetRevision(Guid documentId, int revision, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new DesignDocumentRevisionGetQuery
        {
            Access = OrganizationAccessFilter.GetAccess(HttpContext),
            DocumentId = documentId,
            Revision = revision,
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<DesignDocument>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }
}
