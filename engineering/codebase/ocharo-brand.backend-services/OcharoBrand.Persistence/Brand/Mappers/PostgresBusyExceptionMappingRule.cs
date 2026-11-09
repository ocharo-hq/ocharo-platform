using Microsoft.EntityFrameworkCore;
using Npgsql;
using OcharoBrand.Application.Brand.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoBrand.Persistence.Brand.Mappers;

/// <summary>Maps a write that lost to a concurrent one — a stale revision, a key another request took first, a deadlock, a
/// serialization failure or a lock not granted — to <see cref="BrandErrors.Busy"/>, the conflict the editor retries, as the
/// SQLite lock answered before.</summary>
public sealed class PostgresBusyExceptionMappingRule : IExceptionMappingRule
{
    /// <inheritdoc />
    public AppError? TryMap(Exception exception)
    {
        return exception switch
        {
            DbUpdateConcurrencyException => BrandErrors.Busy(),
            DbUpdateException { InnerException: PostgresException inner } when IsBusy(inner) => BrandErrors.Busy(),
            PostgresException postgres when IsBusy(postgres) => BrandErrors.Busy(),
            _ => null,
        };
    }

    private static bool IsBusy(PostgresException exception) => exception.SqlState is
        PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected
        or PostgresErrorCodes.LockNotAvailable;
}
