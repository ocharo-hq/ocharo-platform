namespace OcharoEvents.Application.Events.Services;

/// <summary>Names the languages the caller's interface prefers, so an event's title shows in the person's own language.</summary>
public interface IUiLocale
{
    /// <summary>Gets the preferred language tags such as <c>uz</c> or <c>ru-RU</c>, most preferred first; empty when the
    /// caller named none.</summary>
    IReadOnlyList<string> Preferred { get; }
}
