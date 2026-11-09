using Microsoft.Data.Sqlite;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoCatalogue.Persistence.Catalogue;

/// <summary>Maps a SQLite busy or locked store (codes 5 and 6), met while another save still holds it, to the
/// catalogue's
/// busy conflict; the caller reloads and retries.</summary>
public sealed class SqliteBusyExceptionMappingRule : IExceptionMappingRule
{
    /// <inheritdoc />
    public AppError? TryMap(Exception exception)
    {
        if (exception is SqliteException { SqliteErrorCode: 5 or 6 })
        {
            return CatalogueErrors.Busy();
        }

        return null;
    }
}
