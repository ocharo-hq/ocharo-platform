using BrandWorkspace.Application.Foundation.Errors;
using Microsoft.AspNetCore.Http.Features;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Api.Foundation.Readers;

/// <summary>Reads a multipart body once, under a route's own request and form limits.</summary>
public static class MultipartFormReader
{
    /// <summary>Reads the form of <paramref name="request"/>, raising the server's body limit to
    /// <paramref name="maximumRequestBytes"/> first.</summary>
    /// <param name="request">The request whose body to read.</param>
    /// <param name="maximumRequestBytes">The largest body the route accepts.</param>
    /// <param name="options">The form limits.</param>
    /// <param name="messages">The messages of each refusal.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    public static async Task<Result<IFormCollection>> ReadAsync(HttpRequest request, long maximumRequestBytes, FormOptions options,
        FormReadMessages messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(messages);
        if (request.ContentLength > maximumRequestBytes)
            return Result<IFormCollection>.Fail(DeskErrors.TooLarge(messages.TooLarge));
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = maximumRequestBytes;
        if (!request.HasFormContentType)
            return Result<IFormCollection>.Fail(DeskErrors.Invalid(messages.NotForm));
        try
        {
            return Result<IFormCollection>.Ok(await request.ReadFormAsync(options, cancellationToken));
        }
        catch (InvalidDataException)
        {
            return Result<IFormCollection>.Fail(DeskErrors.Invalid(messages.Malformed));
        }
        catch (IOException) when (!cancellationToken.IsCancellationRequested && !request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            // MultipartReader reports a completed body with a missing boundary as IOException; an aborted request keeps its
            // transport or cancellation outcome instead of becoming a validation failure.
            return Result<IFormCollection>.Fail(DeskErrors.Invalid(messages.Unterminated));
        }
    }
}
