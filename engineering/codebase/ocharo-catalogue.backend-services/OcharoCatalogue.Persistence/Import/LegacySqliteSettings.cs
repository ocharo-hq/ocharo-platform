namespace OcharoCatalogue.Persistence.Import;

/// <summary>Where the retired SQLite store and its Data Protection key ring sit, resolved to absolute paths by the host.</summary>
/// <remarks>
///   - <see cref="Path"/>: <c>LegacySqlite:Path</c>, else <c>{Catalogue:DataRoot}/ocharo-catalogue.db</c> — the file the
///     image's old <c>ConnectionStrings:OcharoCatalogue</c> named (<c>/data/ocharo-catalogue.db</c> on the
///     <c>catalogue-data</c> volume, <c>App_Data/ocharo-catalogue.db</c> on a local run)
///   - <see cref="KeysPath"/>: <c>LegacySqlite:KeysPath</c>, else <c>DataProtection:KeysPath</c>, else
///     <c>{Catalogue:DataRoot}/keys</c> — the folder the file key ring used
/// </remarks>
public sealed record LegacySqliteSettings
{
    /// <summary>The configuration section that overrides either path.</summary>
    public const string SectionName = "LegacySqlite";

    /// <summary>The SQLite file the import reads and, once verified, retires.</summary>
    public required string Path { get; init; }

    /// <summary>The folder of the file key ring the import copies; it stays in place.</summary>
    public required string KeysPath { get; init; }

    /// <summary>Whether the retired store still waits for <c>import-sqlite</c>.</summary>
    public bool IsWaiting => File.Exists(Path);
}
