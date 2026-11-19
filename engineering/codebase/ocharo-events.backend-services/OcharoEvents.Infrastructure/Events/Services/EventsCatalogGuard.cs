using OcharoEvents.Application.Events.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoEvents.Infrastructure.Events.Services;

/// <summary>Refuses a mirrored call this service does not serve before it reaches WoW2 Events: Events unconfigured (503),
/// or a catalog other than the Events page's own (404).</summary>
public static class EventsCatalogGuard
{
    /// <summary>Gets the refusal for a call on <paramref name="catalog"/>; null when the call is served. A call that names
    /// no catalog, such as the workflow, is refused only while Events is unconfigured.</summary>
    /// <param name="options">The configured Events instance and catalog.</param>
    /// <param name="catalog">The catalog the route names; null for a route outside any catalog.</param>
    public static AppError? Refusal(EventsServiceOptions options, string? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            return OcharoEventsErrors.Unavailable("WoW2 Events is not configured for this service (Events:BaseUrl).");
        return catalog is null || string.Equals(catalog, options.Catalog, StringComparison.Ordinal)
            ? null
            : OcharoEventsErrors.NotFound("The catalog was not found.");
    }
}
