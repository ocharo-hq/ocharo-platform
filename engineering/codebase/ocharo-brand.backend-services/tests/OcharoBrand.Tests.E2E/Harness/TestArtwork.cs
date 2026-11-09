using System.Security.Cryptography;
using SkiaSharp;

namespace OcharoBrand.Tests.Harness;

/// <summary>Generated stand-ins for the approved logo files, and their content hash as the media store keys them.</summary>
internal static class TestArtwork
{
    /// <summary>An 80 x 40 PNG whose colour follows <paramref name="shade"/>; each shade has its own bytes.</summary>
    public static byte[] Png(byte shade)
    {
        using var bitmap = new SKBitmap(80, 40, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(new SKColor(shade, (byte)(255 - shade), 40, 255));
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    /// <summary>The lowercase SHA-256 hex of <paramref name="bytes"/>.</summary>
    public static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
