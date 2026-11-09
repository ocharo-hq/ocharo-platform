using Microsoft.Data.Sqlite;
using OcharoBrand.Application.Brand.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoBrand.Persistence.Brand.Mappers;

/// <summary>Maps a save that met SQLite's lock (<c>SQLITE_BUSY</c>, <c>SQLITE_LOCKED</c>) to <see cref="BrandErrors.Busy"/>, a
/// conflict the editor retries.</summary>
public sealed class SqliteBusyExceptionMappingRule : IExceptionMappingRule
{
    /// <inheritdoc />
    public AppError? TryMap(Exception exception)
    {
        return exception is SqliteException { SqliteErrorCode: 5 or 6 } ? BrandErrors.Busy() : null;
    }
}
