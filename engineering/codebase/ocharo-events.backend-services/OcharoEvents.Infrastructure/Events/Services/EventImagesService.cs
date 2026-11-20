using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OcharoEvents.Application.Events.Errors;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Infrastructure.Events.Services;

/// <summary>
/// Finds where a browser gets an event's image: SCFD's stored copy when WoW2 Files serves it, else the image's own source
/// link. It reads the event through the Events page's mirror (so the catalog guard and the exchanged token's checks hold),
/// takes the file id from <c>facts.images[index].fileRef</c> (never one the browser sent) and asks Files, by the read-only
/// tenant API key, for a short-lived signed URL.
/// </summary>
/// <remarks>An index out of range and an image with no <c>fileRef</c> answer 404. For an image with a <c>fileRef</c>, Files
/// left unconfigured, any Files failure and a Files call that takes longer than <see cref="FilesServiceOptions.LinkTimeout"/>
/// all answer the image's own <c>url</c> instead, only when that is an absolute https link without credentials (else 404),
/// so a card keeps showing the source photo until the copy is served; the log keeps Files' error kind, never its answer.</remarks>
public sealed class EventImagesService(
    EventsApiService events,
    IServiceProvider services,
    IOptions<FilesServiceOptions> files,
    ILogger<EventImagesService> logger)
{
    /// <summary>Holds how long the signed URL stays valid; a browser follows it at once.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Gets the address a browser follows for an event's image: the signed URL to the stored copy, else the source link.</summary>
    /// <param name="catalog">The catalog the route names.</param>
    /// <param name="eventId">The event.</param>
    /// <param name="index">The image's position in the event's <c>facts.images</c>.</param>
    /// <param name="token">The token that cancels the call.</param>
    public async Task<Result<Uri>> GetImageLinkAsync(string catalog, Guid eventId, int index, CancellationToken token)
    {
        var read = await events.GetEventAsync(catalog, eventId, token);
        if (read is not Result<EventModel>.Success { Value: var model })
            return Result<Uri>.Fail(((Result<EventModel>.Failure)read).Error);

        var images = model.Facts.Images;
        if (index < 0 || index >= images.Count || string.IsNullOrWhiteSpace(images[index].FileRef))
            return Result<Uri>.Fail(OcharoEventsErrors.NotFound("The image was not found."));
        var image = images[index];

        if (files.Value.IsConfigured && FileId.TryParse(image.FileRef, out var fileId)
            && await TryIssueAsync(fileId, eventId, token) is { } signed)
            return Result<Uri>.Ok(signed);

        return SourceLink(image.Url) is { } source
            ? Result<Uri>.Ok(source)
            : Result<Uri>.Fail(OcharoEventsErrors.NotFound("The image was not found."));
    }

    /// <summary>Asks Files for the signed URL; null when Files refuses, fails or answers too late. A caller's own
    /// cancellation still cancels the call.</summary>
    private async Task<Uri?> TryIssueAsync(FileId fileId, Guid eventId, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(files.Value.LinkTimeout);
        try
        {
            // Resolved only now: an unconfigured Files client refuses to build, and the copies are optional.
            var urls = services.GetRequiredService<IFileUrlsBroker>();
            var issued = await urls.IssueAsync(
                fileId, new UrlSpec { Lifetime = LinkLifetime, Disposition = FileDisposition.Inline }, timeout.Token);
            if (issued is Result<SignedFileUrl>.Success { Value: var link })
                return link.Url;
            logger.LogWarning(
                "Files refused a signed link for an image of event {EventId} ({ErrorType}); answering the source link.",
                eventId, ((Result<SignedFileUrl>.Failure)issued).Error.Type);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
        {
            logger.LogWarning(
                "Files did not answer for an image of event {EventId} ({ExceptionType}); answering the source link.",
                eventId, exception.GetType().Name);
        }
        return null;
    }

    /// <summary>Reads an image's source <c>url</c> as a link to send a browser to: absolute https, no credentials.</summary>
    private static Uri? SourceLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var link) && link.Scheme == Uri.UriSchemeHttps && link.UserInfo.Length == 0
            ? link
            : null;
}
