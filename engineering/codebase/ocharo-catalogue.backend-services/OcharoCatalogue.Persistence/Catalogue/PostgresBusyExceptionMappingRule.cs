using Npgsql;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoCatalogue.Persistence.Catalogue;

/// <summary>Maps a save PostgreSQL could not serialize beside another one (a serialization failure, a deadlock or a lock
/// not available) to the catalogue's busy conflict; the caller reloads and retries, as with the SQLite store's busy
/// codes before.</summary>
public sealed class PostgresBusyExceptionMappingRule : IExceptionMappingRule
{
    private static readonly HashSet<string> BusyStates =
    [
        PostgresErrorCodes.SerializationFailure,
        PostgresErrorCodes.DeadlockDetected,
        PostgresErrorCodes.LockNotAvailable,
    ];

    /// <inheritdoc />
    public AppError? TryMap(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: var state } && BusyStates.Contains(state))
                return CatalogueErrors.Busy();
        }

        return null;
    }
}
