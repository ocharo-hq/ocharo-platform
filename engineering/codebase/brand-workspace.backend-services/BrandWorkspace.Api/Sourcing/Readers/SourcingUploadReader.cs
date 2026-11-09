using BrandWorkspace.Api.Foundation.Readers;
using BrandWorkspace.Api.Sourcing.Models;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using Microsoft.AspNetCore.Http.Features;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Api.Sourcing.Readers;

/// <summary>Reads one supplier file and its single <c>supplierId</c>, <c>capture</c> and <c>caption</c> fields.</summary>
public static class SourcingUploadReader
{
    /// <summary>Holds the largest upload body: the largest video and room for its fields.</summary>
    public const int MaximumRequestBytes = SourcingMediaInspector.MaximumVideoBytes + 64 * 1024;

    /// <summary>Holds the form limits of an upload.</summary>
    private static readonly FormOptions Limits = new()
    {
        MultipartBodyLengthLimit = MaximumRequestBytes,
        MultipartHeadersLengthLimit = 8192,
        MultipartBoundaryLengthLimit = 128,
        ValueCountLimit = 8,
        ValueLengthLimit = 8192,
    };

    /// <summary>Holds the refusals of an unreadable body.</summary>
    private static readonly FormReadMessages Messages = new("The upload request is too large.",
        "Send one file as multipart form data.", "The multipart form is malformed or exceeds its limits.",
        "The multipart form ended before its final boundary.");

    /// <summary>Reads the upload of <paramref name="request"/>.</summary>
    /// <param name="request">The upload request.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    public static async Task<Result<SourcingUploadForm>> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var read = await MultipartFormReader.ReadAsync(request, MaximumRequestBytes, Limits, Messages, cancellationToken);
        if (read.IsFailure(out var error, out var form))
            return Result<SourcingUploadForm>.Fail(error);
        if (form.Files.Count != 1 || form.Files[0].Name != "file")
            return Result<SourcingUploadForm>.Fail(DeskErrors.Invalid("Send exactly one file in the 'file' field."));
        if (form.Keys.Any(key => key is not ("supplierId" or "capture" or "caption")) || form.Any(field => field.Value.Count > 1))
            return Result<SourcingUploadForm>.Fail(DeskErrors.Invalid("Only single supplierId, capture and caption fields are accepted."));
        Guid? supplierId = null;
        if (!string.IsNullOrWhiteSpace(form["supplierId"]))
        {
            if (!Guid.TryParse(form["supplierId"], out var parsed))
                return Result<SourcingUploadForm>.Fail(DeskErrors.Invalid("Choose a supplier from this organization."));
            supplierId = parsed;
        }
        var file = form.Files[0];
        if (file.Length is <= 0 or > SourcingMediaInspector.MaximumVideoBytes)
            return Result<SourcingUploadForm>.Fail(DeskErrors.TooLarge("Files must contain 1 byte to 64 MiB."));
        using var content = new MemoryStream((int)file.Length);
        await file.CopyToAsync(content, cancellationToken);
        return Result<SourcingUploadForm>.Ok(new SourcingUploadForm(file.FileName, content.ToArray(), supplierId,
            form["capture"].ToString(), form["caption"].ToString()));
    }
}
