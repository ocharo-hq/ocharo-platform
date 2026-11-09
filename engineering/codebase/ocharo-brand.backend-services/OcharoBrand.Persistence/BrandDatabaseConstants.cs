namespace OcharoBrand.Persistence;

/// <summary>Names Brand's place in the product database: its migration set and its schemas.</summary>
public static class BrandDatabaseConstants
{
    /// <summary>The settings key of the product database's connection string, filled per target by Wheelhouse.</summary>
    public const string ConnectionStringName = "Database";

    /// <summary>Brand's migration set, its history, lock and owner marker.</summary>
    public const string MigrationSetName = "brand";

    /// <summary>The schema Brand's set owns in the shared product database.</summary>
    public const string Schema = "brand";

    /// <summary>The schema of Brand's BFF session cache, apart from Desk's <c>cache</c> and Studio's <c>studio_cache</c>: a
    /// cache set claims its schema.</summary>
    public const string CacheSchema = "brand_cache";

    /// <summary>The resource prefix of the set's embedded <c>NNN-name/</c> folders.</summary>
    public const string MigrationResourcePrefix = "OcharoBrand/Migrations/";

    /// <summary>The advisory lock of Brand's set, apart from every other set in the product database.</summary>
    public const long AdvisoryLockId = 8_262_001;
}
