using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Application.Brand.Services;
using OcharoBrand.Domain.Brand;
using SkiaSharp;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Storage.Core;

namespace OcharoBrand.Infrastructure.Brand;

/// <summary>Stores originals once per SHA-256 and a WebP preview (alpha preserved) for fast grids, in the local blob
/// folder. It serves a run with no WoW2 Files (<c>Files:BaseUrl</c> unset), and <see cref="FilesMediaStore"/> reads
/// through it the files stored before Files until the migration fills their ids (research/files-adoption.md #23).</summary>
public sealed class MediaStore(IBrandRepository repository, IBlobRepository blobs, IOptions<BrandOptions> options, TimeProvider time) : IMediaStore
{
    public async Task<Result<MediaBlobRecord>> StoreAsync(byte[] bytes, CancellationToken ct)
    {
        if (MediaBytes.Check(bytes, options.Value.MaximumImageBytes).IsFailure(out var refused, out var contentType))
            return Result<MediaBlobRecord>.Fail(refused);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await repository.FindMediaAsync(sha, ct);
        if (existing is not null) return Result<MediaBlobRecord>.Ok(existing);

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
        return Result<MediaBlobRecord>.Ok(record);
    }

    public async Task<Result<MediaContent>> OpenAsync(string sha256, bool preview, CancellationToken ct)
    {
        var record = MediaBytes.IsSha256(sha256) ? await repository.FindMediaAsync(sha256, ct) : null;
        var content = record is null ? null : await OpenLocalAsync(record, preview, ct);
        return content is null ? Result<MediaContent>.Fail(BrandErrors.NotFound("Media")) : Result<MediaContent>.Ok(content);
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
