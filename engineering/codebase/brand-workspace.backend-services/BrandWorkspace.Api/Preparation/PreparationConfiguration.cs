using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Preparation.Requests;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Application.Preparation.Services;
using BrandWorkspace.Infrastructure.Preparation.Services;
using BrandWorkspace.Persistence.Preparation.Repositories;
using Microsoft.AspNetCore.Http.Features;

namespace BrandWorkspace.Api.Preparation;

/// <summary>Composes the preparation application and its authenticated organization routes.</summary>
public static class PreparationConfiguration
{
    public static IServiceCollection AddPreparationServices(this IServiceCollection services)
    {
        services.AddSingleton<IPreparationRepository, PreparationRepository>();
        services.AddSingleton<IManualPackageBuilder, ManualPackageBuilder>();
        services.AddSingleton<ImageValidationService>();
        services.AddScoped<PreparationService>();
        return services;
    }

    public static WebApplication MapPreparationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizations/{organizationId:guid}")
            .RequireAuthorization()
            .AddEndpointFilter<OrganizationAccessFilter>()
            .AddEndpointFilter<PreparationErrorFilter>();
        group.MapGet("/brands", async (HttpContext context, PreparationService service, CancellationToken token) =>
            Results.Ok(new { data = await service.GetBrandsAsync(OrganizationAccessFilter.GetAccess(context), token) }));
        group.MapPost("/brands", async (CreateBrandRequest request, HttpContext context, PreparationService service, CancellationToken token) =>
            Results.Ok(new { data = await service.CreateBrandAsync(OrganizationAccessFilter.GetAccess(context), request.Name, request.TimeZone, token) }));
        group.MapGet("/media", async (HttpContext context, PreparationService service, CancellationToken token) =>
            Results.Ok(new { data = await service.GetMediaAsync(OrganizationAccessFilter.GetAccess(context), token) }));
        group.MapPost("/media", UploadAsync);
        group.MapGet("/media/{mediaId:guid}/content", async (Guid mediaId, HttpContext context, PreparationService service, CancellationToken token) =>
        {
            var media = await service.GetMediaContentAsync(OrganizationAccessFilter.GetAccess(context), mediaId, token);
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(media.Bytes, media.Asset.ContentType);
        });
        group.MapGet("/drafts", async (Guid? brandId, HttpContext context, PreparationService service, CancellationToken token) =>
            Results.Ok(new { data = await service.GetDraftsAsync(OrganizationAccessFilter.GetAccess(context), brandId, token) }));
        group.MapPost("/drafts", async (SaveDraftRequest request, HttpContext context, PreparationService service, CancellationToken token) =>
            Results.Ok(new { data = await service.SaveDraftAsync(OrganizationAccessFilter.GetAccess(context), null, null, request.ToInput(), token) }));
        group.MapGet("/drafts/{draftId:guid}", async (Guid draftId, HttpContext context, PreparationService service, CancellationToken token) =>
            Results.Ok(new { data = await service.GetDraftAsync(OrganizationAccessFilter.GetAccess(context), draftId, token) }));
        group.MapPut("/drafts/{draftId:guid}", async (Guid draftId, SaveDraftRequest request, HttpContext context, PreparationService service, CancellationToken token) =>
            Results.Ok(new { data = await service.SaveDraftAsync(OrganizationAccessFilter.GetAccess(context), draftId, request.ExpectedRevision, request.ToInput(), token) }));
        group.MapPost("/drafts/{draftId:guid}/request-review",
            async (Guid draftId, RevisionRequest request, HttpContext context, PreparationService service, CancellationToken token) =>
                Results.Ok(new { data = await service.ReviewAsync(OrganizationAccessFilter.GetAccess(context), draftId,
                    request.ExpectedRevision, ReviewAction.Request, null, token) }));
        group.MapPost("/drafts/{draftId:guid}/approve",
            async (Guid draftId, RevisionRequest request, HttpContext context, PreparationService service, CancellationToken token) =>
                Results.Ok(new { data = await service.ReviewAsync(OrganizationAccessFilter.GetAccess(context), draftId,
                    request.ExpectedRevision, ReviewAction.Approve, null, token) }));
        group.MapPost("/drafts/{draftId:guid}/revoke-approval",
            async (Guid draftId, RevisionRequest request, HttpContext context, PreparationService service, CancellationToken token) =>
                Results.Ok(new { data = await service.ReviewAsync(OrganizationAccessFilter.GetAccess(context), draftId,
                    request.ExpectedRevision, ReviewAction.Revoke, request.Reason, token) }));
        group.MapPost("/drafts/{draftId:guid}/exports",
            async (Guid draftId, CreateExportRequest request, HttpContext context, PreparationService service, CancellationToken token) =>
                Results.Ok(new { data = await service.CreateExportAsync(OrganizationAccessFilter.GetAccess(context), draftId,
                    request.ExpectedRevision, request.IdempotencyKey, token) }));
        group.MapGet("/exports/{exportId:guid}/download", async (Guid exportId, HttpContext context, PreparationService service, CancellationToken token) =>
        {
            var bytes = await service.GetExportContentAsync(OrganizationAccessFilter.GetAccess(context), exportId, token);
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(bytes, "application/zip", $"manual-content-{exportId:D}.zip");
        });
        return app;
    }

    private static async Task<IResult> UploadAsync(HttpContext context, PreparationService service, CancellationToken token)
    {
        const int maximumRequestBytes = 6 * 1024 * 1024;
        if (context.Request.ContentLength > maximumRequestBytes)
            throw new PreparationException("too-large", "The upload request is too large.");
        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = maximumRequestBytes;
        if (!context.Request.HasFormContentType)
            throw new PreparationException("validation", "Send one image as multipart form data.");
        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(new FormOptions
            {
                MultipartBodyLengthLimit = ImageValidationService.MaximumBytes,
                MultipartHeadersLengthLimit = 8192,
                MultipartBoundaryLengthLimit = 128,
                ValueCountLimit = 4,
                ValueLengthLimit = 2000
            }, token);
        }
        catch (InvalidDataException)
        {
            throw new PreparationException("validation", "The multipart form is malformed or exceeds its limits.");
        }
        catch (IOException) when (!token.IsCancellationRequested && !context.RequestAborted.IsCancellationRequested)
        {
            // MultipartReader reports a completed body with a missing boundary as IOException.
            // Aborted requests keep their transport/cancellation outcome instead of becoming validation failures.
            throw new PreparationException("validation", "The multipart form ended before its final boundary.");
        }
        if (form.Files.Count != 1 || form.Files[0].Name != "file" || form["provenance"].Count != 1)
            throw new PreparationException("validation", "Send exactly one file and its provenance.");
        var file = form.Files[0];
        if (file.Length is <= 0 or > ImageValidationService.MaximumBytes)
            throw new PreparationException("too-large", "Images must contain 1 byte to 5 MiB.");
        using var content = new MemoryStream();
        await file.CopyToAsync(content, token);
        var asset = await service.CreateMediaAsync(OrganizationAccessFilter.GetAccess(context), file.FileName,
            form["provenance"].ToString(), content.ToArray(), token);
        return Results.Ok(new { data = asset });
    }
}
