using BrandWorkspace.Infrastructure.Preparation.Services;

namespace BrandWorkspace.Api.Foundation;

/// <summary>Verifies the packaged native image decoder with fixed synthetic bytes and no external dependencies.</summary>
public static class MediaRuntimeVerification
{
    public static void Run(ImageValidationService images)
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGMoME0DAAIkAQwba4PpAAAAAElFTkSuQmCC");
        var result = images.Validate("native-runtime-probe.png", bytes);
        if (result.ContentType != "image/png" || result.Sha256 != "5ce44d3259b5bf4b369481e556ceb140f338020b55a8a7ad70214b24da15bfe6")
            throw new InvalidOperationException("The media runtime did not preserve the verified image fixture.");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            mediaRuntime = "ready", contentType = result.ContentType, sha256 = result.Sha256
        }));
    }
}
