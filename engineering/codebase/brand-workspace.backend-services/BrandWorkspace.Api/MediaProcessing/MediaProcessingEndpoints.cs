using Microsoft.AspNetCore.Http.Features;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;

namespace BrandWorkspace.Api.MediaProcessing;

/// <summary>Private image operations for a service caller: no source URL, storage, approval or publication side effects.</summary>
public static class MediaProcessingEndpoints
{
    private const int MaximumBytes = 20 * 1024 * 1024;
    private static readonly SemaphoreSlim Processing = new(1, 1);

    /// <summary>The authorization policy of both routes; it denies everyone until Identity's service tokens release.</summary>
    public const string Policy = "MediaProcessing";

    /// <summary>Maps the routes behind <see cref="Policy"/>, which answers 401 to an anonymous caller and 403 to a signed-in one.</summary>
    public static WebApplication MapMediaProcessingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/integrations/media/background-removal").RequireAuthorization(Policy);
        group.MapGet("/capabilities", async (HttpContext http, IBackgroundRemovalService worker, CancellationToken ct) => {
            http.Response.Headers.CacheControl = "no-store";
            var status = await worker.GetStatusAsync(ct);
            return Results.Ok(status);
        });
        group.MapPost("", ProcessAsync);
        return app;
    }

    private static async Task<IResult> ProcessAsync(HttpContext http, IBackgroundRemovalService worker, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
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
}
