using System.Text.Json;
using System.Text.Json.Serialization;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using Microsoft.AspNetCore.Http.Features;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Api.DesignDocuments.Readers;

/// <summary>Reads an editor document request strictly: one bounded JSON object, unique and known members, exact casing.</summary>
public static class DesignDocumentRequestReader
{
    /// <summary>Holds the largest request body: the largest snapshot and room for its envelope.</summary>
    private const int MaximumRequestBytes = DesignDocumentValidationService.MaximumSnapshotBytes + 4096;

    /// <summary>Holds the strict request JSON.</summary>
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = DesignDocumentValidationService.MaximumDepth + 2,
    };

    /// <summary>Reads the body of <paramref name="request"/> as <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The request type.</typeparam>
    /// <param name="request">The request whose body to read.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    public static async Task<Result<T>> ReadAsync<T>(HttpRequest request, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.HasJsonContentType())
            return Result<T>.Fail(DeskErrors.Invalid("Send a JSON request body."));
        if (request.ContentLength > MaximumRequestBytes)
            return Result<T>.Fail(DeskErrors.TooLarge("The document request exceeds its size limit."));
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = MaximumRequestBytes;
        using var body = new MemoryStream();
        var buffer = new byte[16384];
        while (true)
        {
            var count = await request.Body.ReadAsync(buffer, cancellationToken);
            if (count == 0)
                break;
            if (body.Length + count > MaximumRequestBytes)
                return Result<T>.Fail(DeskErrors.TooLarge("The document request exceeds its size limit."));
            body.Write(buffer, 0, count);
        }
        return Parse<T>(body.GetBuffer().AsMemory(0, (int)body.Length));
    }

    /// <summary>Parses <paramref name="json"/> as one object with unique members.</summary>
    private static Result<T> Parse<T>(ReadOnlyMemory<byte> json)
        where T : class
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = RequestJson.MaxDepth });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Result<T>.Fail(DeskErrors.Invalid("Send a JSON object request body."));
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (document.RootElement.EnumerateObject().Any(property => !names.Add(property.Name)))
                return Result<T>.Fail(DeskErrors.Invalid("Request properties must be unique."));
            return document.RootElement.Deserialize<T>(RequestJson) is { } value
                ? Result<T>.Ok(value)
                : Result<T>.Fail(DeskErrors.Invalid("The document request is missing."));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return Result<T>.Fail(DeskErrors.Invalid("The document request contains malformed or unsupported JSON."));
        }
    }
}
