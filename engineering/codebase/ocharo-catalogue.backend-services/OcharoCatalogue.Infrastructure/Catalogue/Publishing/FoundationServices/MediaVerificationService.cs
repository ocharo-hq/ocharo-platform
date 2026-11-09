using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Media.Enums;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.FoundationServices;

/// <summary>Provides the check that every image a release shows is stored, intact and readable.</summary>
public sealed class MediaVerificationService(IMediaStore media)
{
    /// <summary>
    /// Checks every image in <paramref name="hashes"/>; the first that fails names the error, else null.
    /// </summary>
    /// <param name="hashes">The SHA-256 of each image.</param>
    /// <param name="ct">The token that cancels the check.</param>
    public async Task<AppError?> VerifyAsync(IEnumerable<string> hashes, CancellationToken ct)
    {
        foreach (var hash in hashes.Distinct(StringComparer.Ordinal))
        {
            var check = await media.CheckAsync(hash, ct);
            var error = check switch
            {
                MediaCheck.Missing => CatalogueErrors.Invalid(
                    "A selected catalogue image is missing. Upload it again before publishing."),
                MediaCheck.Corrupt => CatalogueErrors.Invalid(
                    "A selected catalogue image failed its integrity check. Upload it again."),
                MediaCheck.Unavailable => CatalogueErrors.Invalid(
                    "A selected catalogue image is unavailable. Upload it again before publishing."),
                _ => null,
            };
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }
}
