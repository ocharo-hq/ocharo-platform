using OcharoCatalogue.Application.Catalogue.Core.Errors;
using SkiaSharp;

namespace OcharoCatalogue.Infrastructure.Catalogue.Media.Validators;

/// <summary>Validates every stored image, whichever store keeps it.</summary>
internal static class MediaBytes
{
    /// <summary>Returns the image's type, or refuses an empty, oversized or non-image upload.</summary>
    /// <param name="bytes">The uploaded bytes.</param>
    /// <param name="maximumBytes">The largest upload accepted.</param>
    public static string Check(byte[] bytes, long maximumBytes)
    {
        if (bytes.Length == 0 || bytes.LongLength > maximumBytes)
        {
            var message = $"Choose a non-empty image up to {maximumBytes / 1024 / 1024} MB.";
            throw CatalogueErrors.TooLarge(message).ToException();
        }

        return DetectType(bytes)
            ?? throw CatalogueErrors.UnsupportedMediaType("Only PNG, JPEG and WebP images are accepted.").ToException();
    }

    /// <summary>Tells whether a route value is a lowercase SHA-256, the only key a stored file has.</summary>
    /// <param name="value">The route value.</param>
    public static bool IsSha256(string value)
    {
        return value.Length == 64 && value.All(char.IsAsciiHexDigitLower);
    }

    /// <summary>
    /// Reads the image's pixel size from its header, without decoding it; none when it does not parse.
    /// </summary>
    /// <param name="bytes">The image bytes.</param>
    public static (int? Width, int? Height) Dimensions(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return (null, null);
        }

        return (codec.Info.Width, codec.Info.Height);
    }

    /// <summary>Gets the file extension Files checks the type against.</summary>
    /// <param name="contentType">The image's type.</param>
    public static string Extension(string contentType)
    {
        return contentType switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpg",
            _ => "webp",
        };
    }

    private static string? DetectType(byte[] b)
    {
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47)
        {
            return "image/png";
        }

        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (b.Length > 12
            && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
            && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
        {
            return "image/webp";
        }

        return null;
    }
}
