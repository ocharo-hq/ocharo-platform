using BrandWorkspace.Api.Foundation.Readers;
using BrandWorkspace.Api.Preparation.Models;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Preparation.Services;
using Microsoft.AspNetCore.Http.Features;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Api.Preparation.Readers;

/// <summary>Reads one preparation image in the <c>file</c> field and its single <c>provenance</c> field.</summary>
public static class PreparationUploadReader
{
    /// <summary>Holds the largest upload body.</summary>
    private const int MaximumRequestBytes = 6 * 1024 * 1024;

    /// <summary>Holds the form limits of an upload.</summary>
    private static readonly FormOptions Limits = new()
    {
        MultipartBodyLengthLimit = ImageValidationService.MaximumBytes,
        MultipartHeadersLengthLimit = 8192,
        MultipartBoundaryLengthLimit = 128,
        ValueCountLimit = 4,
        ValueLengthLimit = 2000,
    };

    /// <summary>Holds the refusals of an unreadable body.</summary>
    private static readonly FormReadMessages Messages = new("The upload request is too large.",
        "Send one image as multipart form data.", "The multipart form is malformed or exceeds its limits.",
        "The multipart form ended before its final boundary.");

    /// <summary>Reads the upload of <paramref name="request"/>.</summary>
    /// <param name="request">The upload request.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    public static async Task<Result<PreparationUploadForm>> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var read = await MultipartFormReader.ReadAsync(request, MaximumRequestBytes, Limits, Messages, cancellationToken);
        if (read.IsFailure(out var error, out var form))
            return Result<PreparationUploadForm>.Fail(error);
        if (form.Files.Count != 1 || form.Files[0].Name != "file" || form["provenance"].Count != 1)
            return Result<PreparationUploadForm>.Fail(DeskErrors.Invalid("Send exactly one file and its provenance."));
        var file = form.Files[0];
        if (file.Length is <= 0 or > ImageValidationService.MaximumBytes)
            return Result<PreparationUploadForm>.Fail(DeskErrors.TooLarge("Images must contain 1 byte to 5 MiB."));
        using var content = new MemoryStream();
        await file.CopyToAsync(content, cancellationToken);
        return Result<PreparationUploadForm>.Ok(new PreparationUploadForm(file.FileName, form["provenance"].ToString(), content.ToArray()));
    }
}
