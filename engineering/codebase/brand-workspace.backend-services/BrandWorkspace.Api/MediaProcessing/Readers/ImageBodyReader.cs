using BrandWorkspace.Application.Foundation.Errors;
using Microsoft.AspNetCore.Http.Features;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Api.MediaProcessing.Readers;

/// <summary>Reads a raw PNG, JPEG or WebP body of at most 20 MiB.</summary>
public static class ImageBodyReader
{
    /// <summary>Holds the largest image body.</summary>
    private const int MaximumBytes = 20 * 1024 * 1024;

    /// <summary>Holds the refusal of a body over the limit.</summary>
    private const string TooLarge = "Image exceeds 20 MiB.";

    /// <summary>
    /// Refuses a request whose declared length or content type the route does not accept, before it is read.
    /// </summary>
    /// <param name="request">The request to check.</param>
    public static Result Check(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContentLength > MaximumBytes)
            return Result.Fail(DeskErrors.TooLarge(TooLarge));
        return request.ContentType is "image/png" or "image/jpeg" or "image/webp"
            ? Result.Ok()
            : Result.Fail(DeskErrors.UnsupportedMediaType("Send PNG, JPEG or WebP image bytes."));
    }

    /// <summary>Reads the body of <paramref name="request"/>, refusing it once it passes the limit.</summary>
    /// <param name="request">The request whose body to read.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    public static async Task<Result<byte[]>> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature)
            feature.MaxRequestBodySize = MaximumBytes;
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > MaximumBytes)
                return Result<byte[]>.Fail(DeskErrors.TooLarge(TooLarge));
            buffer.Write(chunk, 0, count);
        }
        return Result<byte[]>.Ok(buffer.ToArray());
    }
}
