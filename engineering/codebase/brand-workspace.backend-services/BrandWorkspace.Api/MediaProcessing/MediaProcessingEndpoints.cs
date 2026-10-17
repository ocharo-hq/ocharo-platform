using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Integrations;
using Microsoft.AspNetCore.Http.Features;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;

namespace BrandWorkspace.Api.MediaProcessing;

/// <summary>Private delegated image operations: no source URL, storage, approval or publication side effects.</summary>
public static class MediaProcessingEndpoints
{
    private const int MaximumBytes = 20 * 1024 * 1024;
    private static readonly SemaphoreSlim Processing = new(1, 1);

    /// <summary>Maps HTTPS endpoints authenticated exclusively by a current Marketing or Editorial audience ticket.</summary>
    public static WebApplication MapMediaProcessingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/integrations/media/background-removal")
            .WithMetadata(new SkipAntiforgeryValidation()).AllowAnonymous();
        group.MapGet("/capabilities", async (HttpContext http, IntegrationTickets tickets, IBackgroundRemovalService worker, CancellationToken ct) => {
            http.Response.Headers.CacheControl = "no-store";
            var access = await AuthenticateAsync(http, tickets, ct);
            if (access is null) return Results.Unauthorized();
            var status = await worker.GetStatusAsync(ct);
            return Results.Ok(status);
        });
        group.MapPost("", ProcessAsync);
        return app;
    }

    private static async Task<IResult> ProcessAsync(HttpContext http, IntegrationTickets tickets, IBackgroundRemovalService worker, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        var access = await AuthenticateAsync(http, tickets, ct);
        if (access is null) return Results.Unauthorized();
        if (!access.Membership.CanEdit) return Results.Problem(statusCode: 403, title: "Editor access is required.");
        if (http.Request.ContentLength > MaximumBytes) return Results.Problem(statusCode: 413, title: "Image exceeds 20 MiB.");
        if (http.Request.ContentType is not ("image/png" or "image/jpeg" or "image/webp"))
            return Results.Problem(statusCode: 415, title: "Send PNG, JPEG or WebP image bytes.");
        if (!await Processing.WaitAsync(0, ct)) return Results.Problem(statusCode: 429, title: "Image processing is busy. Try again shortly.");
        try
        {
            if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is {IsReadOnly:false} feature) feature.MaxRequestBodySize = MaximumBytes;
            using var buffer = new MemoryStream(); var chunk = new byte[8192]; int count;
            while ((count = await http.Request.Body.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + count > MaximumBytes) return Results.Problem(statusCode: 413, title: "Image exceeds 20 MiB.");
                buffer.Write(chunk, 0, count);
            }
            var bytes = buffer.ToArray();
            var input = RasterImageGuard.Validate(bytes);
            if (input.ContentType != http.Request.ContentType) return Results.Problem(statusCode: 415, title: "Image bytes do not match the declared type.");
            var result = await worker.RemoveAsync(bytes, ct);
            // A revoked membership/session cannot receive a completed private operation.
            var current = await AuthenticateAsync(http, tickets, ct);
            if (current is null) return Results.Unauthorized();
            if (!current.Membership.CanEdit) return Results.Problem(statusCode: 403, title: "Editor access is required.");
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers["X-Image-Model"] = result.Model;
            http.Response.Headers["X-Image-Width"] = result.Width.ToString(System.Globalization.CultureInfo.InvariantCulture);
            http.Response.Headers["X-Image-Height"] = result.Height.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.File(result.Content, "image/png");
        }
        catch (BackgroundRemovalException e)
        {
            return Results.Problem(statusCode: e.Code == "invalid_image" ? 400 : 503, title: e.Message);
        }
        finally { Processing.Release(); }
    }

    private static async Task<IntegrationAccess?> AuthenticateAsync(HttpContext http, IntegrationTickets tickets, CancellationToken ct)
    {
        if (!http.Request.IsHttps) return null;
        var values = http.Request.Headers["X-Ocharo-Integration-Ticket"];
        if (values.Count != 1) return null;
        return await tickets.ValidateAsync(values[0], "ocharo-marketing", ct)
            ?? await tickets.ValidateAsync(values[0], "ocharo-editorial", ct);
    }
}
