using OcharoCatalogue.Application.Catalogue.Core.Constants;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Web.ErrorMapping;

namespace OcharoCatalogue.Api.Catalogue.Core.Mappers;

/// <summary>Maps a catalogue error to its HTTP status: an upload too large answers 413, one in an unaccepted format
/// 415,
/// a call WoW2 Files refused 502, and every other error the SDK's default status.</summary>
public sealed class CatalogueErrorHttpStatusCodeMapper : IErrorHttpStatusCodeMapper
{
    private readonly ErrorHttpStatusCodeMapper defaults = new();

    /// <inheritdoc />
    public int ToStatusCode(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var reason = error.Metadata?.GetValueOrDefault(CatalogueErrorReasons.Key) as string;
        return reason switch
        {
            CatalogueErrorReasons.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
            CatalogueErrorReasons.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
            CatalogueErrorReasons.UpstreamRefused => StatusCodes.Status502BadGateway,
            _ => defaults.ToStatusCode(error),
        };
    }
}
