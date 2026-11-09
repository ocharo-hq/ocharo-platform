using OcharoBrand.Application.Brand.Constants;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Factories;

namespace OcharoBrand.Application.Brand.Errors;

/// <summary>Creates Brand's application errors; a coded error also names its failure in <see cref="BrandErrorCodes.Key"/>.</summary>
public static class BrandErrors
{
    /// <summary>A command refused on its input.</summary>
    public static AppError Invalid(string message) => AppErrorFactory.Validation(message);

    /// <summary>A text value over its length limit.</summary>
    public static AppError TextTooLong(int maximum) => AppErrorFactory.Validation($"Text is longer than {maximum} characters.");

    /// <summary>A missing item, named by <paramref name="what"/>.</summary>
    public static AppError NotFound(string what) => AppErrorFactory.NotFound($"{what} was not found.");

    /// <summary>A command the current state refuses, such as a stale revision.</summary>
    public static AppError Conflict(string message) => AppErrorFactory.Conflict(message);

    /// <summary>A save that met another save still finishing in the store.</summary>
    public static AppError Busy() => AppErrorFactory.Conflict("Another save is finishing. Reload and retry.");

    /// <summary>An upload that is empty or larger than <paramref name="maximumBytes"/>.</summary>
    public static AppError ImageTooLarge(long maximumBytes) =>
        Coded(AppErrorType.Validation, $"Choose a non-empty image up to {maximumBytes / 1024 / 1024} MB.", BrandErrorCodes.ImageTooLarge);

    /// <summary>An upload that is not a PNG, JPEG or WebP image.</summary>
    public static AppError ImageTypeUnsupported() =>
        Coded(AppErrorType.Validation, "Only PNG, JPEG and WebP images are accepted.", BrandErrorCodes.ImageTypeUnsupported);

    /// <summary>A stored file WoW2 Files no longer holds.</summary>
    public static AppError FilesGone() => AppErrorFactory.NotFound("The file is gone from WoW2 Files.");

    /// <summary>WoW2 Files out of reach.</summary>
    public static AppError FilesUnavailable() => AppErrorFactory.ExternalUnavailable("WoW2 Files is unavailable; try again shortly.");

    /// <summary>A WoW2 Files refusal Brand has no closer kind for.</summary>
    public static AppError FilesRefused(string message) =>
        Coded(AppErrorType.Unexpected, $"WoW2 Files refused the call: {message}", BrandErrorCodes.FilesRefused);

    /// <summary>A private API request without a session.</summary>
    public static AppError SignInRequired() =>
        Coded(AppErrorType.Unauthorized, "Sign in to continue", BrandErrorCodes.SignInRequired);

    /// <summary>A session outside the organization Brand serves.</summary>
    public static AppError OrganizationAccessRequired() =>
        Coded(AppErrorType.Forbidden, "This account is not a member of the Ocharo organization", BrandErrorCodes.OrganizationAccessRequired);

    /// <summary>An unsafe request without <c>brand:edit</c>.</summary>
    public static AppError EditPermissionRequired() =>
        Coded(AppErrorType.Forbidden, "Editing access is required", BrandErrorCodes.EditPermissionRequired);

    /// <summary>A publication without <c>brand:approve</c>.</summary>
    public static AppError ApprovalPermissionRequired() =>
        Coded(AppErrorType.Forbidden, "Publication approval access is required", BrandErrorCodes.ApprovalPermissionRequired);

    /// <summary>A session route reached over plain HTTP.</summary>
    public static AppError HttpsRequired() =>
        Coded(AppErrorType.Validation, "HTTPS is required", BrandErrorCodes.HttpsRequired);

    private static AppError Coded(AppErrorType type, string message, string code)
    {
        return AppError.Of(type, message, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [BrandErrorCodes.Key] = code,
            [AppErrorProblemDetailsFactory.ExtensionsMetadataKey] =
                new Dictionary<string, object?>(StringComparer.Ordinal) { [BrandErrorCodes.Key] = code },
        });
    }
}
