using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.DesignDocuments.Requests;
using BrandWorkspace.Api.Preparation;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;

namespace BrandWorkspace.Api.DesignDocuments;

/// <summary>Composes durable editor documents behind the existing organization and antiforgery boundaries.</summary>
public static class DesignDocumentConfiguration
{
    private const int MaximumRequestBytes = DesignDocumentValidationService.MaximumSnapshotBytes + 4096;
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = DesignDocumentValidationService.MaximumDepth + 2
    };

    public static WebApplication MapDesignDocumentEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/organizations/{organizationId:guid}/design-documents")
            .RequireAuthorization()
            .AddEndpointFilter<OrganizationAccessFilter>()
            .AddEndpointFilter<PreparationErrorFilter>();
        group.MapGet("", GetAsync);
        group.MapPost("", CreateAsync);
        group.MapGet("/{documentId:guid}", GetByIdAsync);
        group.MapPut("/{documentId:guid}", SaveAsync);
        group.MapPost("/{documentId:guid}/archive", ArchiveAsync);
        group.MapGet("/{documentId:guid}/revisions", GetRevisionsAsync);
        group.MapGet("/{documentId:guid}/revisions/{revision:int}", GetRevisionAsync);
        return app;
    }

    private static async Task<IResult> GetAsync(
        string? kind,
        bool? includeArchived,
        int? limit,
        int? offset,
        HttpContext context,
        DesignDocumentService service,
        CancellationToken token)
    {
        IReadOnlyList<DesignDocumentSummary> documents = await service.GetDocumentsAsync(
            OrganizationAccessFilter.GetAccess(context),
            kind,
            includeArchived ?? false,
            limit ?? 50,
            offset ?? 0,
            token);
        return Results.Ok(new { data = documents });
    }

    private static async Task<IResult> GetByIdAsync(
        Guid documentId,
        HttpContext context,
        DesignDocumentService service,
        CancellationToken token)
    {
        DesignDocument document = await service.GetDocumentAsync(
            OrganizationAccessFilter.GetAccess(context),
            documentId,
            token);
        return Results.Ok(new { data = document });
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        DesignDocumentService service,
        CancellationToken token)
    {
        CreateDesignDocumentRequest request = await ReadRequestAsync<CreateDesignDocumentRequest>(context, token);
        DesignDocument document = await service.CreateDocumentAsync(
            OrganizationAccessFilter.GetAccess(context),
            request.Kind,
            request.ToInput(),
            token);
        return Results.Ok(new { data = document });
    }

    private static async Task<IResult> SaveAsync(
        Guid documentId,
        HttpContext context,
        DesignDocumentService service,
        CancellationToken token)
    {
        SaveDesignDocumentRequest request = await ReadRequestAsync<SaveDesignDocumentRequest>(context, token);
        DesignDocument document = await service.SaveDocumentAsync(
            OrganizationAccessFilter.GetAccess(context),
            documentId,
            request.ExpectedRevision,
            request.ToInput(),
            token);
        return Results.Ok(new { data = document });
    }

    private static async Task<IResult> ArchiveAsync(
        Guid documentId,
        HttpContext context,
        DesignDocumentService service,
        CancellationToken token)
    {
        ArchiveDesignDocumentRequest request = await ReadRequestAsync<ArchiveDesignDocumentRequest>(context, token);
        DesignDocument document = await service.ArchiveDocumentAsync(
            OrganizationAccessFilter.GetAccess(context),
            documentId,
            request.ExpectedRevision,
            token);
        return Results.Ok(new { data = document });
    }

    private static async Task<IResult> GetRevisionsAsync(
        Guid documentId,
        int? limit,
        int? offset,
        HttpContext context,
        DesignDocumentService service,
        CancellationToken token)
    {
        IReadOnlyList<DesignDocumentRevision> revisions = await service.GetRevisionsAsync(
            OrganizationAccessFilter.GetAccess(context),
            documentId,
            limit ?? 50,
            offset ?? 0,
            token);
        return Results.Ok(new { data = revisions });
    }

    private static async Task<IResult> GetRevisionAsync(
        Guid documentId,
        int revision,
        HttpContext context,
        DesignDocumentService service,
        CancellationToken token)
    {
        DesignDocument document = await service.GetRevisionAsync(
            OrganizationAccessFilter.GetAccess(context),
            documentId,
            revision,
            token);
        return Results.Ok(new { data = document });
    }

    private static async Task<T> ReadRequestAsync<T>(HttpContext context, CancellationToken token)
    {
        if (!context.Request.HasJsonContentType())
            throw new PreparationException("validation", "Send a JSON request body.");
        if (context.Request.ContentLength > MaximumRequestBytes)
            throw new PreparationException("too-large", "The document request exceeds its size limit.");
        IHttpMaxRequestBodySizeFeature? feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = MaximumRequestBytes;
        using var body = new MemoryStream();
        var buffer = new byte[16384];
        while (true)
        {
            int count = await context.Request.Body.ReadAsync(buffer, token);
            if (count == 0) break;
            if (body.Length + count > MaximumRequestBytes)
                throw new PreparationException("too-large", "The document request exceeds its size limit.");
            body.Write(buffer, 0, count);
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                body.GetBuffer().AsMemory(0, (int)body.Length),
                new JsonDocumentOptions { MaxDepth = RequestJson.MaxDepth });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new PreparationException("validation", "Send a JSON object request body.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new PreparationException("validation", "Request properties must be unique.");
            }
            return document.RootElement.Deserialize<T>(RequestJson)
                ?? throw new PreparationException("validation", "The document request is missing.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new PreparationException("validation", "The document request contains malformed or unsupported JSON.");
        }
    }
}
