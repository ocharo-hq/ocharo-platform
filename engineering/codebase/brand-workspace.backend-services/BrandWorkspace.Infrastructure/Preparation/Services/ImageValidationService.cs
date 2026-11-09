using BrandWorkspace.Application.Foundation.Errors;
using System.Security.Cryptography;
using BrandWorkspace.Application.Preparation.Models;
using SkiaSharp;

namespace BrandWorkspace.Infrastructure.Preparation.Services;

/// <summary>Validates bounded PNG/JPEG structure before storing the original immutable bytes.</summary>
public sealed class ImageValidationService
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    public const int MaximumDimension = 8192;
    public const long MaximumPixels = 20_000_000;

    public ValidatedImage Validate(string fileName, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 200 || fileName is "." or ".."
            || fileName.Any(character => char.IsControl(character) || "/\\:".Contains(character)))
            throw DeskErrors.Invalid("Use a file name without directory or control characters.").ToException();
        if (bytes.Length == 0) throw DeskErrors.Invalid("The image is empty.").ToException();
        if (bytes.Length > MaximumBytes) throw DeskErrors.TooLarge("Images must be at most 5 MiB.").ToException();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) throw DeskErrors.Invalid("The image has an invalid structure.").ToException();
        {
            if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg))
                throw DeskErrors.Invalid("Only PNG and JPEG images are supported.").ToException();
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || info.Width > MaximumDimension || info.Height > MaximumDimension
                || (long)info.Width * info.Height > MaximumPixels || codec.FrameCount > 1)
                throw DeskErrors.Invalid("Use a single image up to 8192 pixels per side and 20 megapixels.").ToException();
            // Decoding validates compressed pixel data as well as dimensions and format headers.
            using var decoded = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
                throw DeskErrors.Invalid("The image could not be completely decoded.").ToException();
            if (codec.EncodedFormat == SKEncodedImageFormat.Png)
            {
                ReadOnlySpan<byte> end = [0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130];
                if (bytes.Length < end.Length || !bytes.AsSpan(bytes.Length - end.Length).SequenceEqual(end))
                    throw DeskErrors.Invalid("The PNG has an invalid ending.").ToException();
            }
            else if (bytes.Length < 2 || bytes[^2] != 0xff || bytes[^1] != 0xd9)
                throw DeskErrors.Invalid("The JPEG has an invalid ending.").ToException();
            return new ValidatedImage(bytes, codec.EncodedFormat == SKEncodedImageFormat.Png ? "image/png" : "image/jpeg",
                Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
    }
}
