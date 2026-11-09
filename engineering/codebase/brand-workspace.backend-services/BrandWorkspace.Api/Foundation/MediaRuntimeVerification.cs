using BrandWorkspace.Infrastructure.Preparation.Services;

namespace BrandWorkspace.Api.Foundation;

/// <summary>Verifies the packaged native image decoder with fixed synthetic bytes and no external dependencies.</summary>
public static class MediaRuntimeVerification
{
    /// <summary>Decodes the fixture, writes the one-line JSON result to <paramref name="output"/> and returns the exit code.</summary>
    /// <param name="images">The image validation the API serves with.</param>
    /// <param name="output">The verb's standard output.</param>
    /// <returns><c>0</c>; a decoder that changes the fixture throws instead.</returns>
    public static int Run(ImageValidationService images, TextWriter output)
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGMoME0DAAIkAQwba4PpAAAAAElFTkSuQmCC");
        var result = images.Validate("native-runtime-probe.png", bytes);
        if (result.ContentType != "image/png" || result.Sha256 != "5ce44d3259b5bf4b369481e556ceb140f338020b55a8a7ad70214b24da15bfe6")
            throw new InvalidOperationException("The media runtime did not preserve the verified image fixture.");
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            mediaRuntime = "ready", contentType = result.ContentType, sha256 = result.Sha256
        }));
        return 0;
    }
}
