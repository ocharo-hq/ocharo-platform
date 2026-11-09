using System.Runtime.CompilerServices;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace BrandWorkspace.Application.Foundation.Errors;

/// <summary>Creates Desk's application errors; each message is safe to show the caller, and the HTTP boundary maps each
/// kind to a status.</summary>
public static class DeskErrors
{
    /// <summary>A request refused on its input.</summary>
    public static AppError Invalid(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Validation, message, null, member, file, line);
    }

    /// <summary>A body or file over its size limit.</summary>
    public static AppError TooLarge(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.PayloadTooLarge, message, null, member, file, line);
    }

    /// <summary>A body whose content type the route does not accept.</summary>
    public static AppError UnsupportedMediaType(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.UnsupportedMediaType, message, null, member, file, line);
    }

    /// <summary>A record missing from the caller's organization.</summary>
    public static AppError NotFound(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.NotFound, message, null, member, file, line);
    }

    /// <summary>A change the current state refuses, such as a stale revision.</summary>
    public static AppError Conflict(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Conflict, message, null, member, file, line);
    }

    /// <summary>An action the caller's organization role does not permit.</summary>
    public static AppError Forbidden(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Forbidden, message, null, member, file, line);
    }

    /// <summary>A request without a signed-in person.</summary>
    public static AppError Unauthorized(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Unauthorized, message, null, member, file, line);
    }

    /// <summary>A request refused while the same work is still running.</summary>
    public static AppError Busy(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.TooManyRequests, message, null, member, file, line);
    }

    /// <summary>A dependency, such as WoW2 Files or the image worker, out of reach.</summary>
    public static AppError Unavailable(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.ExternalUnavailable, message, null, member, file, line);
    }

    /// <summary>A call WoW2 Files refused for a reason Desk has no closer kind for.</summary>
    public static AppError Upstream(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.ExternalRejected, message, null, member, file, line);
    }
}
