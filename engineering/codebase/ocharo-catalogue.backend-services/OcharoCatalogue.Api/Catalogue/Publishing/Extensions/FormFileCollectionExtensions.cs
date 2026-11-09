using OcharoCatalogue.Api.Catalogue.Core.Extensions;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;

namespace OcharoCatalogue.Api.Catalogue.Publishing.Extensions;

/// <summary>Extends a form's uploaded files with reading them as release artifacts.</summary>
public static class FormFileCollectionExtensions
{
    /// <summary>Reads every uploaded file, whatever its form field, in upload order.</summary>
    /// <param name="files">The form's files.</param>
    /// <param name="ct">The token that cancels the read.</param>
    public static async Task<IReadOnlyList<ReleaseUpload>> ReadUploadsAsync(
        this IFormFileCollection files,
        CancellationToken ct)
    {
        var uploads = new List<ReleaseUpload>(files.Count);
        foreach (var file in files)
        {
            uploads.Add(new ReleaseUpload(file.FileName, await file.ReadAllBytesAsync(ct)));
        }

        return uploads;
    }
}
