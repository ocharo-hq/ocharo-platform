using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OcharoBrand.Application.Brand;
using OcharoBrand.Domain.Brand;
using SkiaSharp;
using WoW.Two.Sdk.Beta.Be.Storage.Core;

namespace OcharoBrand.Infrastructure.Brand;

/// <summary>Stores originals once per SHA-256 and a WebP preview (alpha preserved) for fast grids, in the local blob
/// folder. It serves a run with no WoW2 Files (<c>Files:BaseUrl</c> unset), and <see cref="FilesMediaStore"/> reads
/// through it the files stored before Files until the migration fills their ids (planning/files-adoption.md #23).</summary>
public sealed class MediaStore(IBrandRepository repository, IBlobRepository blobs, IOptions<BrandOptions> options, TimeProvider time) : IMediaStore
{
    public async Task<MediaBlobRecord> StoreAsync(byte[] bytes, CancellationToken ct)
    {
        var contentType = MediaBytes.Check(bytes, options.Value.MaximumImageBytes);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await repository.FindMediaAsync(sha, ct);
        if (existing is not null) return existing;

        var path = $"media/{sha[..2]}/{sha}";
        await using (var input = new MemoryStream(bytes, false))
            await blobs.SaveAsync(path, input, contentType, ct);

        int? width = null, height = null;
        string? previewPath = null;
        using (var bitmap = SKBitmap.Decode(bytes))
        {
            if (bitmap is not null)
            {
                width = bitmap.Width;
                height = bitmap.Height;
                var preview = Preview(bitmap, options.Value.PreviewSize);
                if (preview is not null)
                {
                    previewPath = $"previews/{sha[..2]}/{sha}.webp";
                    await using var previewStream = new MemoryStream(preview, false);
                    await blobs.SaveAsync(previewPath, previewStream, "image/webp", ct);
                }
            }
        }

        var record = new MediaBlobRecord
        {
            Sha256 = sha,
            ContentType = contentType,
            ByteLength = bytes.LongLength,
            BlobPath = path,
            PreviewPath = previewPath,
            Width = width,
            Height = height,
            StoredAt = time.GetUtcNow(),
        };
        await repository.SaveMediaAsync(record, ct);
        return record;
    }

    public async Task<MediaContent?> OpenAsync(string sha256, bool preview, CancellationToken ct)
    {
        if (!MediaBytes.IsSha256(sha256)) return null;
        var record = await repository.FindMediaAsync(sha256, ct);
        return record is null ? null : await OpenLocalAsync(record, preview, ct);
    }

    /// <summary>Opens a stored file's bytes from the local blob folder; none once they are gone from it.</summary>
    internal async Task<MediaContent?> OpenLocalAsync(MediaBlobRecord record, bool preview, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(record.BlobPath)) return null;
        var fromPreview = preview && record.PreviewPath is not null;
        var stream = await blobs.OpenReadAsync(fromPreview ? record.PreviewPath! : record.BlobPath, ct);
        return stream is null ? null : new MediaContent(record, fromPreview ? "image/webp" : record.ContentType, null, stream);
    }

    private static byte[]? Preview(SKBitmap source, int longest)
    {
        var scale = Math.Min(1.0, longest / (double)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        using var resized = scale < 1.0 ? source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKCubicResampler.Mitchell)) : source.Copy();
        if (resized is null) return null;
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 82);
        return data?.ToArray();
    }
}

/// <summary>The checks every stored image passes, whichever store keeps it.</summary>
internal static class MediaBytes
{
    /// <summary>Returns the image's type, or refuses an empty, oversized or non-image upload.</summary>
    public static string Check(byte[] bytes, long maximumBytes)
    {
        if (bytes.Length == 0 || bytes.LongLength > maximumBytes)
            throw new BrandException($"Choose a non-empty image up to {maximumBytes / 1024 / 1024} MB.", 413);
        return DetectType(bytes) ?? throw new BrandException("Only PNG, JPEG and WebP images are accepted.", 415);
    }

    /// <summary>Tells whether a route value is a lowercase SHA-256, the only key a stored file has.</summary>
    public static bool IsSha256(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigitLower);

    /// <summary>Reads the image's pixel size from its header, without decoding it; none when it does not parse.</summary>
    public static (int? Width, int? Height) Dimensions(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        return codec is null ? (null, null) : (codec.Info.Width, codec.Info.Height);
    }

    /// <summary>The file extension Files checks the type against.</summary>
    public static string Extension(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        _ => "webp",
    };

    private static string? DetectType(byte[] b)
    {
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        return null;
    }
}
