using System.Runtime.CompilerServices;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoEvents.Application.Events.Errors;

/// <summary>Creates the Ocharo Events service's application errors; each message is safe to show the caller, and the HTTP
/// boundary maps each kind to a status.</summary>
public static class OcharoEventsErrors
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

    /// <summary>A record missing from the caller's organization, or an organization the caller cannot read.</summary>
    public static AppError NotFound(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.NotFound, message, null, member, file, line);
    }

    /// <summary>A write that moved elsewhere for good, such as an event's facts moving to WoW2 Events' review.</summary>
    public static AppError Gone(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Gone, message, null, member, file, line);
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

    /// <summary>An action the caller's organization permissions do not allow.</summary>
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

    /// <summary>A session or organization route reached over plain HTTP.</summary>
    public static AppError HttpsRequired(
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.Validation, "HTTPS is required", null, member, file, line);
    }

    /// <summary>WoW2 Events out of reach, or not configured for this host.</summary>
    public static AppError Unavailable(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.ExternalUnavailable, message, null, member, file, line);
    }

    /// <summary>A WoW2 Events answer the service has no closer kind for.</summary>
    public static AppError Upstream(
        string message,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
    {
        return AppError.Of(AppErrorType.ExternalRejected, message, null, member, file, line);
    }
}
