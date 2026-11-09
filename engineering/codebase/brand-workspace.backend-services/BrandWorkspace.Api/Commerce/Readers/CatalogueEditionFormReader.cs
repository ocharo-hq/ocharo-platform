using System.Text.Json;
using BrandWorkspace.Api.Commerce.Models;
using BrandWorkspace.Api.Foundation.Readers;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Commerce.Services;
using BrandWorkspace.Persistence.Commerce.Serializers;
using Microsoft.AspNetCore.Http.Features;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Api.Commerce.Readers;

/// <summary>Reads a catalogue edition upload: one <c>metadata</c> field naming the saved design revision, and its
/// artifacts, each within its size limit.</summary>
public static class CatalogueEditionFormReader
{
    /// <summary>Holds the largest upload body: every artifact and room for the form.</summary>
    private const int MaximumRequestBytes = CatalogueEditionBuilder.MaximumTotalBytes + 1024 * 1024;

    /// <summary>Holds the metadata members, each exactly once.</summary>
    private static readonly string[] MetadataMembers = ["expectedRevision", "sourceDesignId", "sourceRevision"];

    /// <summary>Holds the form limits of an upload.</summary>
    private static readonly FormOptions Limits = new()
    {
        MultipartBodyLengthLimit = CatalogueEditionBuilder.MaximumPdfBytes,
        MultipartHeadersLengthLimit = 8192,
        MultipartBoundaryLengthLimit = 128,
        ValueCountLimit = 202,
        ValueLengthLimit = 2000,
    };

    /// <summary>Holds the refusals of an unreadable body.</summary>
    private static readonly FormReadMessages Messages = new("Catalogue upload exceeds 256 MiB.",
        "Send catalogue artifacts as multipart form data.", "The catalogue form is malformed or exceeds its limits.",
        "The catalogue form ended before its final boundary.");

    /// <summary>Reads the upload of <paramref name="request"/>.</summary>
    /// <param name="request">The upload request.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    public static async Task<Result<CatalogueEditionForm>> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var read = await MultipartFormReader.ReadAsync(request, MaximumRequestBytes, Limits, Messages, cancellationToken);
        if (read.IsFailure(out var error, out var form))
            return Result<CatalogueEditionForm>.Fail(error);
        if (form.Count != 1 || form["metadata"].Count != 1 || form.Files.Count > 201)
            return Result<CatalogueEditionForm>.Fail(DeskErrors.Invalid("Send metadata and the exact catalogue file set."));
        var parsed = Metadata(form["metadata"].ToString());
        if (parsed.IsFailure(out error, out var metadata))
            return Result<CatalogueEditionForm>.Fail(error);
        if (metadata.ExpectedRevision <= 0 || metadata.SourceRevision <= 0 || metadata.SourceDesignId == Guid.Empty)
            return Result<CatalogueEditionForm>.Fail(DeskErrors.Invalid("Saved source and draft revisions are required."));
        if (form.Files.Sum(file => file.Length) > CatalogueEditionBuilder.MaximumTotalBytes)
            return Result<CatalogueEditionForm>.Fail(DeskErrors.Invalid("Catalogue artifacts exceed 256 MiB."));
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var file in form.Files)
        {
            var limit = file.Name == "pdf" ? CatalogueEditionBuilder.MaximumPdfBytes : CatalogueEditionBuilder.MaximumPageBytes;
            if (file.Name.Length > 120 || files.ContainsKey(file.Name) || file.Length <= 0 || file.Length > limit)
                return Result<CatalogueEditionForm>.Fail(DeskErrors.Invalid("Catalogue files must be distinct and within their size limits."));
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            files.Add(file.Name, buffer.ToArray());
        }
        return Result<CatalogueEditionForm>.Ok(new CatalogueEditionForm(metadata, files));
    }

    /// <summary>Parses the metadata field: one object with exactly its three members.</summary>
    private static Result<CatalogueEditionUpload> Metadata(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var value = document.RootElement;
            if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != MetadataMembers.Length
                || !value.EnumerateObject().Select(member => member.Name).ToHashSet().SetEquals(MetadataMembers))
                return Result<CatalogueEditionUpload>.Fail(DeskErrors.Invalid("Catalogue metadata has unknown or duplicate fields."));
            return value.Deserialize<CatalogueEditionUpload>(CommerceJson.Options) is { } metadata
                ? Result<CatalogueEditionUpload>.Ok(metadata)
                : Result<CatalogueEditionUpload>.Fail(DeskErrors.Invalid("Catalogue metadata is invalid."));
        }
        catch (JsonException)
        {
            return Result<CatalogueEditionUpload>.Fail(DeskErrors.Invalid("Catalogue metadata is invalid."));
        }
    }
}
