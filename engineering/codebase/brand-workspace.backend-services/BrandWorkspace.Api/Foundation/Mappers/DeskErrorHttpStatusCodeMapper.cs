using BrandWorkspace.Application.Foundation.Constants;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Web.ErrorMapping;

namespace BrandWorkspace.Api.Foundation.Mappers;

/// <summary>Maps Desk's errors to HTTP statuses: a <see cref="DeskErrorReasons"/> reason decides where the error kind
/// alone does not, and the SDK's mapping decides the rest.</summary>
public sealed class DeskErrorHttpStatusCodeMapper : IErrorHttpStatusCodeMapper
{
    /// <summary>Holds the SDK's mapping by error kind.</summary>
    private readonly ErrorHttpStatusCodeMapper kinds = new();

    /// <inheritdoc />
    public int ToStatusCode(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var reason = error.Metadata?.GetValueOrDefault(DeskErrorReasons.Key) as string;
        return reason switch
        {
            DeskErrorReasons.TooLarge => StatusCodes.Status413PayloadTooLarge,
            DeskErrorReasons.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
            DeskErrorReasons.Upstream => StatusCodes.Status502BadGateway,
            _ => kinds.ToStatusCode(error),
        };
    }
}
