using OcharoBrand.Application.Brand.Errors;
using SkiaSharp;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoBrand.Infrastructure.Brand;

/// <summary>The checks every stored image passes, whichever store keeps it.</summary>
internal static class MediaBytes
{
    /// <summary>Returns the image's type, or refuses an empty, oversized or non-image upload.</summary>
    public static Result<string> Check(byte[] bytes, long maximumBytes)
    {
        if (bytes.Length == 0 || bytes.LongLength > maximumBytes)
            return Result<string>.Fail(BrandErrors.ImageTooLarge(maximumBytes));
        return DetectType(bytes) is { } type ? Result<string>.Ok(type) : Result<string>.Fail(BrandErrors.ImageTypeUnsupported());
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
