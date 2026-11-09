using OcharoCatalogue.Application.Catalogue.Core.Constants;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoCatalogue.Application.Catalogue.Core.Errors;

/// <summary>Creates the catalogue's application errors; the delivery boundary maps each to its HTTP status.</summary>
public static class CatalogueErrors
{
    /// <summary>Creates the error of a command or query whose input breaks a catalogue rule.</summary>
    /// <param name="message">The rule the input breaks, as the person reads it.</param>
    public static AppError Invalid(string message)
    {
        return AppError.Of(AppErrorType.Validation, message);
    }

    /// <summary>Creates the error of a catalogue item that does not exist.</summary>
    /// <param name="subject">The item, such as <c>Product</c>.</param>
    public static AppError NotFound(string subject)
    {
        return AppError.Of(AppErrorType.NotFound, $"{subject} was not found.");
    }

    /// <summary>
    /// Creates the error of a catalogue item that is gone, in the words of <paramref name="message"/>.
    /// </summary>
    /// <param name="message">What is gone, as the person reads it.</param>
    public static AppError Missing(string message)
    {
        return AppError.Of(AppErrorType.NotFound, message);
    }

    /// <summary>Creates the error of a write that lost to another write or to the item's current state.</summary>
    /// <param name="message">What changed, and what the person does next.</param>
    public static AppError Conflict(string message)
    {
        return AppError.Of(AppErrorType.Conflict, message);
    }

    /// <summary>Creates the error of a save that met another save still holding the store.</summary>
    public static AppError Busy()
    {
        return Conflict("Another save is finishing. Reload and retry.");
    }

    /// <summary>Creates the error of an upload larger than the catalogue accepts.</summary>
    /// <param name="message">The limit, as the person reads it.</param>
    public static AppError TooLarge(string message)
    {
        return Reasoned(
            AppErrorType.Validation,
            message,
            CatalogueErrorReasons.PayloadTooLarge);
    }

    /// <summary>Creates the error of an upload in a format the catalogue does not accept.</summary>
    /// <param name="message">The accepted formats, as the person reads them.</param>
    public static AppError UnsupportedMediaType(string message)
    {
        return Reasoned(
            AppErrorType.Validation,
            message,
            CatalogueErrorReasons.UnsupportedMediaType);
    }

    /// <summary>Creates the error of WoW2 Files being unreachable just now.</summary>
    /// <param name="message">What to do, as the person reads it.</param>
    public static AppError FilesUnavailable(string message)
    {
        return AppError.Of(AppErrorType.ExternalUnavailable, message);
    }

    /// <summary>Creates the error of a call WoW2 Files refused.</summary>
    /// <param name="message">The refusal, as the person reads it.</param>
    public static AppError FilesRefused(string message)
    {
        return Reasoned(
            AppErrorType.ExternalUnavailable,
            message,
            CatalogueErrorReasons.UpstreamRefused);
    }

    private static AppError Reasoned(
        AppErrorType type,
        string message,
        string reason)
    {
        var metadata = new Dictionary<string, object?> { [CatalogueErrorReasons.Key] = reason };
        return AppError.Of(
            type,
            message,
            metadata);
    }
}
