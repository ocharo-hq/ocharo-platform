using OcharoCatalogue.Api.Catalogue.Core.Constants;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Factories;

namespace OcharoCatalogue.Api.Catalogue.Core.Errors;

/// <summary>Creates the errors the catalogue's HTTP boundary refuses a request with before any handler runs; each
/// carries its machine reason as a problem extension.</summary>
public static class CatalogueHttpErrors
{
    /// <summary>Creates the error of a browser write sent from an origin the catalogue does not trust.</summary>
    public static AppError CrossOriginWrite()
    {
        return Reasoned(
            AppErrorType.Forbidden,
            "Cross-origin writes are disabled.",
            CatalogueProblemReasons.CrossOriginWrite);
    }

    /// <summary>
    /// Creates the error of a sign-in request sent over plain HTTP, where the secure cookie cannot hold.
    /// </summary>
    public static AppError HttpsRequired()
    {
        return Reasoned(
            AppErrorType.Validation,
            "HTTPS is required.",
            CatalogueProblemReasons.HttpsRequired);
    }

    private static AppError Reasoned(
        AppErrorType type,
        string message,
        string reason)
    {
        var extensions = new Dictionary<string, object?> { ["reason"] = reason };
        var metadata = new Dictionary<string, object?>
        {
            [AppErrorProblemDetailsFactory.ExtensionsMetadataKey] = extensions,
        };
        return AppError.Of(
            type,
            message,
            metadata);
    }
}
