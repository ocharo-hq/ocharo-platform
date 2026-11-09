namespace OcharoBrand.Persistence.Import;

/// <summary>Where the retired SQLite store, its Data Protection key ring and its local blob folder sit; the host resolves
/// each to an absolute path from today's settings.</summary>
public sealed record LegacySqliteSettings
{
    /// <summary>The configuration section the host binds.</summary>
    public const string SectionName = "LegacySqlite";

    /// <summary>The SQLite file the import reads: <c>App_Data/ocharo-brand.db</c> under the content root, the image's
    /// <c>/data/ocharo-brand.db</c> on the <c>brand-data</c> volume.</summary>
    public string Path { get; init; } = "App_Data/ocharo-brand.db";

    /// <summary>The folder of the file key ring the import copies: the image's <c>/data/keys</c>.</summary>
    public string KeysPath { get; init; } = "App_Data/keys";

    /// <summary>The local blob folder of files stored before WoW2 Files (<c>Brand:DataRoot</c>/<c>blobs</c>), whose bytes
    /// the import checks against each media row's SHA-256; it moves nothing.</summary>
    public string BlobsPath { get; init; } = "App_Data/blobs";

    /// <summary>Tells whether the retired store still waits for the import; Brand's routes answer 503 meanwhile.</summary>
    public bool Waiting => File.Exists(Path);
}
