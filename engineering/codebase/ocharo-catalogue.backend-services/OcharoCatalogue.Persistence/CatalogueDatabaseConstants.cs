namespace OcharoCatalogue.Persistence;

/// <summary>Names Catalogue's place in the product database: its migration set, its schemas and its advisory lock.</summary>
public static class CatalogueDatabaseConstants
{
    /// <summary>The settings key of the product database's connection string, filled per target by Wheelhouse.</summary>
    public const string ConnectionStringName = "Database";

    /// <summary>Catalogue's migration set, its history, lock and owner marker.</summary>
    public const string MigrationSetName = "catalogue";

    /// <summary>The schema Catalogue's set owns in the shared product database.</summary>
    public const string Schema = "catalogue";

    /// <summary>The schema of Catalogue's BFF session cache, apart from Desk's <c>cache</c> and Studio's
    /// <c>studio_cache</c>: a cache set claims its schema.</summary>
    public const string CacheSchema = "catalogue_cache";

    /// <summary>The resource prefix of the set's embedded <c>NNN-name/</c> folders.</summary>
    public const string MigrationResourcePrefix = "OcharoCatalogue/Migrations/";

    /// <summary>The advisory lock of Catalogue's set, apart from every other set in the product database.</summary>
    public const long AdvisoryLockId = 8_278_001;
}
