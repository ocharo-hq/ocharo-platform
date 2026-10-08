using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Preparation;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using Microsoft.AspNetCore.Http.Features;

namespace BrandWorkspace.Api.Sourcing;

public sealed record SupplierRequest(string? Name, string? Stage, string? Country, string? City, string? Website, string? Notes);
public sealed record UpdateSupplierRequest(
    int ExpectedRevision, string? Name, string? Stage, string? Country, string? City, string? Website, string? Notes);
public sealed record PersonRequest(string? Name, string? Role, string? Notes);
public sealed record UpdatePersonRequest(int ExpectedRevision, string? Name, string? Role, string? Notes);
public sealed record RouteRequest(
    string? Channel, string? Destination, string? Label, string? SourceUrl, bool Verified, bool IsPrimary, Guid? PersonId, string? Notes);
public sealed record UpdateRouteRequest(
    int ExpectedRevision, string? Channel, string? Destination, string? Label, string? SourceUrl, bool Verified, bool IsPrimary,
    Guid? PersonId, string? Notes);
public sealed record UpdateMediaRequest(int ExpectedRevision, Guid? SupplierId, string? Capture, string? Caption);
public sealed record AssignMediaRequest(IReadOnlyList<Guid>? MediaIds, Guid? SupplierId);

/// <summary>Maps supplier, contact and shared-media routes behind organization membership and antiforgery.</summary>
public static class SourcingEndpoints
{
    private const int MaximumUploadRequestBytes = SourcingMediaInspector.MaximumVideoBytes + 64 * 1024;

    public static WebApplication MapSourcingEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/organizations/{organizationId:guid}/sourcing")
            .RequireAuthorization()
            .AddEndpointFilter<OrganizationAccessFilter>()
            .AddEndpointFilter<PreparationErrorFilter>();

        group.MapGet("/suppliers", async (string? query, string? stage, int? limit, int? offset, HttpContext context,
                SourcingService service, CancellationToken token) =>
            Results.Ok(new { data = await service.GetSuppliersAsync(Access(context), query, stage, limit ?? 50, offset ?? 0, token) }));
        group.MapPost("/suppliers", async (SupplierRequest request, HttpContext context, SourcingService service,
                SourcingValidationService validation, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.CreateSupplierAsync(Access(context), validation.Supplier(
                    request.Name, request.Stage, request.Country, request.City, request.Website, request.Notes), token)
            }));
        group.MapGet("/suppliers/{supplierId:guid}", async (Guid supplierId, HttpContext context, SourcingService service,
                CancellationToken token) =>
            Results.Ok(new { data = await service.GetSupplierAsync(Access(context), supplierId, token) }));
        group.MapPut("/suppliers/{supplierId:guid}", async (Guid supplierId, UpdateSupplierRequest request, HttpContext context,
                SourcingService service, SourcingValidationService validation, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.UpdateSupplierAsync(Access(context), supplierId, request.ExpectedRevision, validation.Supplier(
                    request.Name, request.Stage, request.Country, request.City, request.Website, request.Notes), token)
            }));

        group.MapPost("/suppliers/{supplierId:guid}/people", async (Guid supplierId, PersonRequest request, HttpContext context,
                SourcingService service, SourcingValidationService validation, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.CreatePersonAsync(Access(context), supplierId,
                    validation.Person(request.Name, request.Role, request.Notes), token)
            }));
        group.MapPut("/people/{personId:guid}", async (Guid personId, UpdatePersonRequest request, HttpContext context,
                SourcingService service, SourcingValidationService validation, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.UpdatePersonAsync(Access(context), personId, request.ExpectedRevision,
                    validation.Person(request.Name, request.Role, request.Notes), token)
            }));
        group.MapDelete("/people/{personId:guid}", async (Guid personId, int expectedRevision, HttpContext context,
            SourcingService service, CancellationToken token) =>
        {
            await service.DeletePersonAsync(Access(context), personId, expectedRevision, token);
            return Results.Ok(new { data = new { id = personId } });
        });

        group.MapPost("/suppliers/{supplierId:guid}/routes", async (Guid supplierId, RouteRequest request, HttpContext context,
                SourcingService service, SourcingValidationService validation, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.CreateRouteAsync(Access(context), supplierId, validation.Route(request.Channel,
                    request.Destination, request.Label, request.SourceUrl, request.Verified, request.IsPrimary, request.PersonId,
                    request.Notes), token)
            }));
        group.MapPut("/routes/{routeId:guid}", async (Guid routeId, UpdateRouteRequest request, HttpContext context,
                SourcingService service, SourcingValidationService validation, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.UpdateRouteAsync(Access(context), routeId, request.ExpectedRevision, validation.Route(
                    request.Channel, request.Destination, request.Label, request.SourceUrl, request.Verified, request.IsPrimary,
                    request.PersonId, request.Notes), token)
            }));
        group.MapDelete("/routes/{routeId:guid}", async (Guid routeId, int expectedRevision, HttpContext context,
            SourcingService service, CancellationToken token) =>
        {
            await service.DeleteRouteAsync(Access(context), routeId, expectedRevision, token);
            return Results.Ok(new { data = new { id = routeId } });
        });

        group.MapGet("/media", async (Guid? supplierId, bool? unassigned, int? limit, int? offset, HttpContext context,
                SourcingService service, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.GetMediaAsync(Access(context), supplierId, unassigned ?? false, limit ?? 100, offset ?? 0, token)
            }));
        group.MapGet("/media/summary", async (HttpContext context, SourcingService service, CancellationToken token) =>
            Results.Ok(new { data = await service.GetMediaTotalsAsync(Access(context), token) }));
        group.MapPost("/media", UploadAsync);
        group.MapPost("/media/assign", async (AssignMediaRequest request, HttpContext context, SourcingService service,
                CancellationToken token) =>
            Results.Ok(new { data = new { updated = await service.AssignMediaAsync(Access(context), request.MediaIds, request.SupplierId, token) } }));
        group.MapPut("/media/{mediaId:guid}", async (Guid mediaId, UpdateMediaRequest request, HttpContext context,
                SourcingService service, SourcingValidationService validation, CancellationToken token) =>
            Results.Ok(new
            {
                data = await service.UpdateMediaAsync(Access(context), mediaId, request.ExpectedRevision,
                    validation.Media(request.SupplierId, request.Capture, request.Caption), token)
            }));
        group.MapDelete("/media/{mediaId:guid}", async (Guid mediaId, int expectedRevision, HttpContext context,
            SourcingService service, CancellationToken token) =>
        {
            await service.DeleteMediaAsync(Access(context), mediaId, expectedRevision, token);
            return Results.Ok(new { data = new { id = mediaId } });
        });
        group.MapGet("/media/{mediaId:guid}/content", async (Guid mediaId, HttpContext context, SourcingService service,
            CancellationToken token) =>
        {
            var content = await service.GetMediaContentAsync(Access(context), mediaId, token);
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(content.Bytes, content.ContentType, enableRangeProcessing: true);
        });
        group.MapGet("/media/{mediaId:guid}/thumbnail", async (Guid mediaId, HttpContext context, SourcingService service,
            CancellationToken token) =>
        {
            byte[] thumbnail = await service.GetMediaThumbnailAsync(Access(context), mediaId, token);
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(thumbnail, "image/webp");
        });
        return app;
    }

    private static async Task<IResult> UploadAsync(HttpContext context, SourcingService service, CancellationToken token)
    {
        if (context.Request.ContentLength > MaximumUploadRequestBytes)
            throw new PreparationException("too-large", "The upload request is too large.");
        IHttpMaxRequestBodySizeFeature? sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false }) sizeFeature.MaxRequestBodySize = MaximumUploadRequestBytes;
        if (!context.Request.HasFormContentType)
            throw new PreparationException("validation", "Send one file as multipart form data.");
        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(new FormOptions
            {
                MultipartBodyLengthLimit = MaximumUploadRequestBytes,
                MultipartHeadersLengthLimit = 8192,
                MultipartBoundaryLengthLimit = 128,
                ValueCountLimit = 8,
                ValueLengthLimit = 8192
            }, token);
        }
        catch (InvalidDataException)
        {
            throw new PreparationException("validation", "The multipart form is malformed or exceeds its limits.");
        }
        catch (IOException) when (!token.IsCancellationRequested && !context.RequestAborted.IsCancellationRequested)
        {
            throw new PreparationException("validation", "The multipart form ended before its final boundary.");
        }
        if (form.Files.Count != 1 || form.Files[0].Name != "file")
            throw new PreparationException("validation", "Send exactly one file in the 'file' field.");
        if (form.Keys.Any(key => key is not ("supplierId" or "capture" or "caption")) || form.Any(field => field.Value.Count > 1))
            throw new PreparationException("validation", "Only single supplierId, capture and caption fields are accepted.");
        Guid? supplierId = null;
        if (!string.IsNullOrWhiteSpace(form["supplierId"]))
        {
            supplierId = Guid.TryParse(form["supplierId"], out Guid parsed) ? parsed
                : throw new PreparationException("validation", "Choose a supplier from this organization.");
        }
        IFormFile file = form.Files[0];
        if (file.Length is <= 0 or > SourcingMediaInspector.MaximumVideoBytes)
            throw new PreparationException("too-large", "Files must contain 1 byte to 64 MiB.");
        using var content = new MemoryStream((int)file.Length);
        await file.CopyToAsync(content, token);
        var upload = await service.UploadMediaAsync(Access(context), file.FileName, content.ToArray(), supplierId,
            form["capture"].ToString(), form["caption"].ToString(), token);
        return Results.Ok(new { data = upload });
    }

    private static Application.Access.OrganizationAccess Access(HttpContext context) => OrganizationAccessFilter.GetAccess(context);
}
