using BrandWorkspace.Application.Foundation.Constants;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using System.Security.Cryptography;
using AwesomeAssertions;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Infrastructure.Preparation.Services;
using SkiaSharp;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>Exercises actual codecs and adversarial inputs at the media trust boundary.</summary>
public sealed class PreparationImageTests
{
    private readonly ImageValidationService _validator = new();

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpg", "image/jpeg")]
    public void Validate_ShouldPreserveBytesAndDigest_ForCompleteImages(string extension, string contentType)
    {
        var bytes = ImageBytes(extension);
        var result = _validator.Validate($"image.{extension}", bytes);
        result.Bytes.Should().Equal(bytes);
        result.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        result.ContentType.Should().Be(contentType);
    }

    [Theory]
    [InlineData("../photo.png")]
    [InlineData("..\\photo.png")]
    [InlineData("/photo.png")]
    [InlineData("C:photo.png")]
    [InlineData("bad\nphoto.png")]
    public void Validate_ShouldRejectTraversalOrControlNames(string name)
    {
        var act = () => _validator.Validate(name, ImageBytes("png"));
        act.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
    }

    [Fact]
    public void Validate_ShouldRejectUnsupportedAndMalformedBytes()
    {
        byte[][] invalid =
        [
            [],
            "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray(),
            "GIF89a"u8.ToArray(),
            ImageBytes("png")[..24],
            ImageBytes("jpg")[..20]
        ];
        foreach (var bytes in invalid)
        {
            var act = () => _validator.Validate("image.png", bytes);
            act.Should().Throw<AppException>();
        }
        var corrupt = ImageBytes("png");
        var idat = corrupt.AsSpan().IndexOf("IDAT"u8);
        corrupt[idat + 4] ^= 0xff;
        var corruptAct = () => _validator.Validate("image.png", corrupt);
        corruptAct.Should().Throw<AppException>();
    }

    [Fact]
    public void Validate_ShouldRejectOversizedAndHugeDimensionInputs()
    {
        var oversized = new byte[ImageValidationService.MaximumBytes + 1];
        var tooBig = () => _validator.Validate("image.png", oversized);
        tooBig.Should().Throw<AppException>().Which.Error.Metadata![DeskErrorReasons.Key].Should().Be(DeskErrorReasons.TooLarge);
        using var wide = new SKBitmap(ImageValidationService.MaximumDimension + 1, 1);
        wide.Erase(SKColors.Blue);
        using var wideImage = SKImage.FromBitmap(wide);
        using var output = wideImage.Encode(SKEncodedImageFormat.Png, 100);
        var tooWide = () => _validator.Validate("wide.png", output.ToArray());
        tooWide.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
    }

    private static byte[] ImageBytes(string extension)
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.Erase(new SKColor(30, 90, 160, 255));
        using var image = SKImage.FromBitmap(bitmap);
        using var output = image.Encode(extension == "png" ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 100);
        return output.ToArray();
    }
}
