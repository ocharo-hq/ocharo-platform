using Microsoft.Extensions.Options;
using OcharoEvents.Application.Events.Errors;
using OcharoEvents.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Infrastructure.Delivery.Services;

/// <summary>
/// Serves the organization's Telegram delivery from WoW2 Events' scheduled digests of the Events page's catalog: the
/// definitions, a preview, a send now and the run history (tasks-platform.md O17). Every call acts for the signed-in
/// person through the broker's token exchange, so Events keeps each digest to the session's organization and checks
/// <c>events:admin</c> again; Events renders and sends each message through Ocharo's Identity bot.
/// </summary>
/// <remarks>Only the configured catalog is served: another slug answers 404 before reaching Events, and an unconfigured
/// Events answers 503, as the events page does.</remarks>
public sealed class EventDigestsService(IEventDigestsBroker digests, IOptions<EventsServiceOptions> options)
{
    /// <summary>Lists the organization's digests of the catalog.</summary>
    public Task<Result<IReadOnlyList<DigestModel>>> ListAsync(string catalog, CancellationToken token) =>
        Served(catalog) is { } refused
            ? Task.FromResult(Result<IReadOnlyList<DigestModel>>.Fail(refused))
            : digests.ListAsync(options.Value.Catalog, token);

    /// <summary>Gets one digest.</summary>
    public Task<Result<DigestModel>> GetAsync(string catalog, Guid id, CancellationToken token) =>
        Served(catalog) is { } refused ? Refuse<DigestModel>(refused) : digests.GetAsync(options.Value.Catalog, id, token);

    /// <summary>Creates a digest for the organization.</summary>
    public Task<Result<DigestModel>> CreateAsync(string catalog, DigestSpec spec, CancellationToken token) =>
        Served(catalog) is { } refused ? Refuse<DigestModel>(refused) : digests.CreateAsync(options.Value.Catalog, spec, token);

    /// <summary>Replaces a digest at the revision the page read.</summary>
    public Task<Result<DigestModel>> UpdateAsync(string catalog, Guid id, DigestSpec spec, CancellationToken token) =>
        Served(catalog) is { } refused
            ? Refuse<DigestModel>(refused)
            : digests.UpdateAsync(options.Value.Catalog, id, spec, token);

    /// <summary>Deletes a digest and its run history at the revision the page read.</summary>
    public Task<Result<DigestModel>> DeleteAsync(string catalog, Guid id, int expectedRevision, CancellationToken token) =>
        Served(catalog) is { } refused
            ? Refuse<DigestModel>(refused)
            : digests.DeleteAsync(options.Value.Catalog, id, expectedRevision, token);

    /// <summary>Renders the digest now without sending: the texts a send now would send.</summary>
    public Task<Result<DigestPreviewModel>> PreviewAsync(string catalog, Guid id, CancellationToken token) =>
        Served(catalog) is { } refused
            ? Refuse<DigestPreviewModel>(refused)
            : digests.PreviewAsync(options.Value.Catalog, id, token);

    /// <summary>Runs the digest now, outside its schedule, and sends it to every recipient.</summary>
    public Task<Result<DigestRunModel>> SendNowAsync(string catalog, Guid id, CancellationToken token) =>
        Served(catalog) is { } refused
            ? Refuse<DigestRunModel>(refused)
            : digests.SendNowAsync(options.Value.Catalog, id, token);

    /// <summary>Lists the digest's runs, newest first.</summary>
    public Task<Result<DigestRunPage>> ListRunsAsync(string catalog, Guid id, DigestRunQuery query, CancellationToken token) =>
        Served(catalog) is { } refused
            ? Refuse<DigestRunPage>(refused)
            : digests.ListRunsAsync(options.Value.Catalog, id, query, token);

    /// <summary>Refuses a call this service does not serve: Events unconfigured, or a catalog other than its own.</summary>
    private AppError? Served(string catalog)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl))
            return OcharoEventsErrors.Unavailable("WoW2 Events is not configured for this service (Events:BaseUrl).");
        return string.Equals(catalog, options.Value.Catalog, StringComparison.Ordinal)
            ? null
            : OcharoEventsErrors.NotFound("The catalog was not found.");
    }

    private static Task<Result<T>> Refuse<T>(AppError error) where T : notnull => Task.FromResult(Result<T>.Fail(error));
}
