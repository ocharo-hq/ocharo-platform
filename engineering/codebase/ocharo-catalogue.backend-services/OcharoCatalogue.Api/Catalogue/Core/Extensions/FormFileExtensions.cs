namespace OcharoCatalogue.Api.Catalogue.Core.Extensions;

/// <summary>Extends an uploaded form file with reading its bytes for an application command.</summary>
public static class FormFileExtensions
{
    /// <summary>Reads every byte of <paramref name="file"/>.</summary>
    /// <param name="file">The uploaded file.</param>
    /// <param name="ct">The token that cancels the read.</param>
    public static async Task<byte[]> ReadAllBytesAsync(this IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
