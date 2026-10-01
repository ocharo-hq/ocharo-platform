using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Entities;
using SkiaSharp;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace OcharoCatalogue.Infrastructure.Catalogue;

/// <summary>Stores originals once per SHA-256 and a WebP preview (alpha preserved) for fast grids.</summary>
public sealed class MediaStore(ICatalogueRepository repository, IBlobRepository blobs, IOptions<CatalogueOptions> options, IClock clock) : IMediaStore
{
    public async Task<MediaBlobRecord> StoreAsync(byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length == 0 || bytes.LongLength > options.Value.MaximumImageBytes)
            throw new CatalogueException($"Choose a non-empty image up to {options.Value.MaximumImageBytes / 1024 / 1024} MB.", 413);
        var contentType = DetectType(bytes) ?? throw new CatalogueException("Only PNG, JPEG and WebP images are accepted.", 415);
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
            StoredAt = clock.UtcNow,
        };
        await repository.SaveMediaAsync(record, ct);
        return record;
    }

    public async Task<(MediaBlobRecord Record, Stream Content)?> OpenAsync(string sha256, bool preview, CancellationToken ct)
    {
        if (sha256.Length != 64 || !sha256.All(char.IsAsciiHexDigitLower)) return null;
        var record = await repository.FindMediaAsync(sha256, ct);
        if (record is null) return null;
        var path = preview && record.PreviewPath is not null ? record.PreviewPath : record.BlobPath;
        var stream = await blobs.OpenReadAsync(path, ct);
        return stream is null ? null : (record, stream);
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

    private static string? DetectType(byte[] b)
    {
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        return null;
    }
}
