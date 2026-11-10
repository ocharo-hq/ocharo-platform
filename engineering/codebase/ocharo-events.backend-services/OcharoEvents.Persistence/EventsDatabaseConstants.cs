namespace OcharoEvents.Persistence;

/// <summary>Names the Events service's place in the product database: its migration set and its schemas.</summary>
public static class EventsDatabaseConstants
{
    /// <summary>The settings key of the product database's connection string, filled per target by Wheelhouse.</summary>
    public const string ConnectionStringName = "Database";

    /// <summary>The service's migration set, its history, lock and owner marker.</summary>
    public const string MigrationSetName = "ocharo_events";

    /// <summary>The schema the set owns in the shared product database; it holds only the Data Protection key ring.</summary>
    public const string Schema = "ocharo_events";

    /// <summary>The schema of the BFF session cache, apart from Desk's <c>cache</c>, Brand's <c>brand_cache</c> and
    /// Catalogue's <c>catalogue_cache</c>: a cache set claims its schema.</summary>
    public const string CacheSchema = "ocharo_events_cache";

    /// <summary>The resource prefix of the set's embedded <c>NNN-name/</c> folders.</summary>
    public const string MigrationResourcePrefix = "OcharoEvents/Migrations/";

    /// <summary>The advisory lock of the set, apart from every other set in the product database (the API's port, 8290).</summary>
    public const long AdvisoryLockId = 8_290_001;
}
