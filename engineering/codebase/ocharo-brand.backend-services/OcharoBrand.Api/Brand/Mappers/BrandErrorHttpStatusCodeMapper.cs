using OcharoBrand.Application.Brand.Constants;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Web.ErrorMapping;

namespace OcharoBrand.Api.Brand.Mappers;

/// <summary>Maps Brand's coded errors to the statuses no SDK error kind carries — 413, 415 and 502 — and every other error
/// as the SDK does.</summary>
public sealed class BrandErrorHttpStatusCodeMapper : IErrorHttpStatusCodeMapper
{
    private readonly ErrorHttpStatusCodeMapper sdk = new();

    /// <inheritdoc />
    public int ToStatusCode(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Metadata?.GetValueOrDefault(BrandErrorCodes.Key) switch
        {
            BrandErrorCodes.ImageTooLarge => StatusCodes.Status413PayloadTooLarge,
            BrandErrorCodes.ImageTypeUnsupported => StatusCodes.Status415UnsupportedMediaType,
            BrandErrorCodes.FilesRefused => StatusCodes.Status502BadGateway,
            _ => sdk.ToStatusCode(error),
        };
    }
}
