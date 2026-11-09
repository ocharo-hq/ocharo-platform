using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using BrandWorkspace.Application.Foundation.Errors;
using System.Security.Cryptography;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Media.Images;

namespace BrandWorkspace.Infrastructure.Sourcing.Services;

/// <summary>Identifies supplier media by content, bounds it, and derives previews and fingerprints from images.</summary>
public sealed class SourcingMediaInspector(IImageService images)
{
    public const int MaximumImageBytes = 20 * 1024 * 1024;
    public const int MaximumPdfBytes = 25 * 1024 * 1024;
    public const int MaximumVideoBytes = 64 * 1024 * 1024;
    public const int MaximumDimension = 8192;
    public const long MaximumPixels = 50_000_000;
    public const int ThumbnailSize = 480;

    public async Task<InspectedMedia> InspectAsync(byte[] bytes, CancellationToken token)
    {
        if (bytes.Length == 0) throw DeskErrors.Invalid("The file is empty.").ToException();
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (bytes.AsSpan().StartsWith("%PDF-"u8))
        {
            if (bytes.Length > MaximumPdfBytes) throw DeskErrors.TooLarge("PDFs must be at most 25 MiB.").ToException();
            return new InspectedMedia(bytes, sha256, "application/pdf", null, null, null, null, false, null);
        }
        if (bytes.Length >= 12 && bytes.AsSpan(4, 4).SequenceEqual("ftyp"u8))
        {
            if (bytes.Length > MaximumVideoBytes) throw DeskErrors.TooLarge("Videos must be at most 64 MiB.").ToException();
            return new InspectedMedia(bytes, sha256, "video/mp4", null, null, null, null, false, null);
        }
        if (bytes.Length > MaximumImageBytes) throw DeskErrors.TooLarge("Images must be at most 20 MiB.").ToException();
        try
        {
            ImageProbeResult probe = await images.ProbeAsync(new MemoryStream(bytes, false), token);
            string contentType = probe.Format switch
            {
                ImageFormat.Jpeg => "image/jpeg",
                ImageFormat.Png => "image/png",
                ImageFormat.Webp => "image/webp",
                _ => throw Unsupported()
            };
            if (probe.FrameCount > 1) throw DeskErrors.Invalid("Animated images are not supported.").ToException();
            if (probe.Width is < 1 or > MaximumDimension || probe.Height is < 1 or > MaximumDimension
                || (long)probe.Width * probe.Height > MaximumPixels)
                throw DeskErrors.Invalid("Use an image up to 8192 pixels per side and 50 megapixels.").ToException();
            ImageResult thumbnail = await images.EditAsync(new MemoryStream(bytes, false), new ImageEditSpec
            {
                AutoOrient = true,
                Resize = new ImageResizeSpec { Width = ThumbnailSize, Height = ThumbnailSize, Fit = ImageFit.Max, Upscale = false },
                Output = new ImageOutputSpec { Format = ImageFormat.Webp, Quality = 78 }
            }, token);
            ImageMetadataResult? metadata = await ReadMetadataAsync(bytes, token);
            ImageAnalysisResult analysis = await images.AnalyzeAsync(new MemoryStream(bytes, false), token);
            bool rotated = thumbnail.Width != thumbnail.Height && (thumbnail.Width > thumbnail.Height) != (probe.Width > probe.Height);
            return new InspectedMedia(
                bytes,
                sha256,
                contentType,
                rotated ? probe.Height : probe.Width,
                rotated ? probe.Width : probe.Height,
                thumbnail.Content.ToArray(),
                metadata?.TakenAtWithOffset?.ToUniversalTime(),
                metadata?.HasLocation ?? false,
                unchecked((long)analysis.PerceptualHash));
        }
        catch (ImageRejectedException)
        {
            throw Unsupported();
        }
    }

    private async Task<ImageMetadataResult?> ReadMetadataAsync(byte[] bytes, CancellationToken token)
    {
        try { return await images.ReadMetadataAsync(new MemoryStream(bytes, false), token); }
        catch (ImageRejectedException) { return null; }
    }

    private static AppException Unsupported() =>
        DeskErrors.Invalid("Only JPEG, PNG and WebP images, PDFs and MP4 videos are supported.").ToException();
}
