using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Media.Enums;
using OcharoCatalogue.Application.Catalogue.Media.Models;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Options;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Validators;
using SkiaSharp;
using WoW.Two.Sdk.Beta.Be.Storage.Core;

namespace OcharoCatalogue.Infrastructure.Catalogue.Media.Stores;

/// <summary>Stores originals once per SHA-256 and a WebP preview (alpha preserved) for fast grids, in the local blob
/// folder. It serves a run with no WoW2 Files (<c>Files:BaseUrl</c> unset), and <see cref="FilesMediaStore"/> reads
/// through it the files stored before Files until the migration fills their ids (planning/files-adoption.md
/// #24).</summary>
public sealed class MediaStore(
    ICatalogueRepository repository,
    IBlobRepository blobs,
    IOptions<CatalogueOptions> options,
    TimeProvider timeProvider) : IMediaStore
{
    /// <inheritdoc />
    public async Task<MediaBlobRecord> StoreAsync(byte[] bytes, CancellationToken ct)
    {
        var contentType = MediaBytes.Check(bytes, options.Value.MaximumImageBytes);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await repository.FindMediaAsync(sha, ct);
        if (existing is not null)
        {
            return existing;
        }

        var path = $"media/{sha[..2]}/{sha}";
        await using (var input = new MemoryStream(bytes, false))
        {
            await blobs.SaveAsync(
                path,
                input,
                contentType,
                ct);
        }

        int? width = null;
        int? height = null;
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
                    await blobs.SaveAsync(
                        previewPath,
                        previewStream,
                        "image/webp",
                        ct);
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
            StoredAt = timeProvider.GetUtcNow(),
        };
        await repository.SaveMediaAsync(record, ct);
        return record;
    }

    /// <inheritdoc />
    public async Task<MediaContent?> OpenAsync(
        string sha256,
        bool preview,
        CancellationToken ct)
    {
        if (!MediaBytes.IsSha256(sha256))
        {
            return null;
        }

        var record = await repository.FindMediaAsync(sha256, ct);
        if (record is null)
        {
            return null;
        }

        return await OpenLocalAsync(
            record,
            preview,
            ct);
    }

    /// <inheritdoc />
    public async Task<MediaCheck> CheckAsync(string sha256, CancellationToken ct)
    {
        if (!MediaBytes.IsSha256(sha256))
        {
            return MediaCheck.Missing;
        }

        var record = await repository.FindMediaAsync(sha256, ct);
        if (record is null)
        {
            return MediaCheck.Missing;
        }

        return await CheckLocalAsync(record, ct);
    }

    /// <summary>Hashes a stored file's local bytes against its SHA-256.</summary>
    /// <param name="record">The stored file.</param>
    /// <param name="ct">The token that cancels the read.</param>
    internal async Task<MediaCheck> CheckLocalAsync(MediaBlobRecord record, CancellationToken ct)
    {
        try
        {
            var opened = await OpenLocalAsync(
                record,
                false,
                ct);
            if (opened?.Content is not { } stream)
            {
                return MediaCheck.Missing;
            }

            await using (stream)
            {
                var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
                return hash == record.Sha256 ? MediaCheck.Ok : MediaCheck.Corrupt;
            }
        }
        catch (IOException)
        {
            return MediaCheck.Unavailable;
        }
    }

    /// <summary>Opens a stored file's bytes from the local blob folder; none once they are gone from it.</summary>
    /// <param name="record">The stored file.</param>
    /// <param name="preview">Whether the web preview is wanted rather than the original.</param>
    /// <param name="ct">The token that cancels the read.</param>
    internal async Task<MediaContent?> OpenLocalAsync(
        MediaBlobRecord record,
        bool preview,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(record.BlobPath))
        {
            return null;
        }

        var fromPreview = preview && record.PreviewPath is not null;
        var stream = await blobs.OpenReadAsync(fromPreview ? record.PreviewPath! : record.BlobPath, ct);
        if (stream is null)
        {
            return null;
        }

        return new MediaContent(
            record,
            fromPreview ? "image/webp" : record.ContentType,
            null,
            stream);
    }

    private static byte[]? Preview(SKBitmap source, int longest)
    {
        var scale = Math.Min(1.0, longest / (double)Math.Max(source.Width, source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var info = new SKImageInfo(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        using var resized = scale < 1.0
            ? source.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell))
            : source.Copy();
        if (resized is null)
        {
            return null;
        }

        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Webp, 82);
        return data?.ToArray();
    }
}
