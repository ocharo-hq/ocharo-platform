namespace OcharoBrand.Persistence.Import;

/// <summary>How an import ended: <c>imported</c>, <c>current</c> and <c>planned</c> exit 0, <c>refused</c> exits 1.</summary>
public enum LegacySqliteImportStatus
{
    Imported,
    Current,
    Planned,
    Refused,
}

/// <summary>What one run of the import read, wrote and verified; it carries counts and digests, never documents, ids or
/// keys.</summary>
public sealed record LegacySqliteImportReport
{
    public required LegacySqliteImportStatus Status { get; init; }

    /// <summary>One line on the outcome, for an operator.</summary>
    public required string Summary { get; init; }

    /// <summary>Each table of the store, in copy order.</summary>
    public IReadOnlyList<LegacySqliteTableReport> Tables { get; init; } = [];

    /// <summary>The rows across every table of the SQLite store.</summary>
    public int Rows { get; init; }

    /// <summary>The rows this run inserted; the rest were already in PostgreSQL, identical.</summary>
    public int Copied { get; init; }

    /// <summary>The rows whose PostgreSQL copy is missing or differs, plus the rows only PostgreSQL holds; any refuses the
    /// run.</summary>
    public int Diverged { get; init; }

    /// <summary>The releases the store holds; release 1 is the first public kit.</summary>
    public int Releases { get; init; }

    /// <summary>The media rows whose bytes live in WoW2 Files under a recorded file id.</summary>
    public int FileIds { get; init; }

    /// <summary>The media rows with a local blob from before WoW2 Files.</summary>
    public int Blobs { get; init; }

    /// <summary>The local blobs whose bytes hash to their row's SHA-256.</summary>
    public int BlobsVerified { get; init; }

    /// <summary>The local blobs whose bytes hash to something else; any refuses the run.</summary>
    public int BlobsDiverged { get; init; }

    /// <summary>The media rows with neither a file id nor a local blob: unreadable before the copy as after it.</summary>
    public int BlobsMissing { get; init; }

    /// <summary>SHA-256 over every media row's SHA-256 and its local blob's, in SHA-256 order.</summary>
    public string? BlobsSha256 { get; init; }

    /// <summary>The key files in the file key ring.</summary>
    public int Keys { get; init; }

    /// <summary>The keys this run inserted.</summary>
    public int KeysCopied { get; init; }

    /// <summary>The keys PostgreSQL lacks or holds differently after the copy; any refuses the run.</summary>
    public int KeysMissing { get; init; }

    /// <summary>Where the SQLite file was moved once verified, relative to its folder.</summary>
    public string? RetiredTo { get; init; }
}

/// <summary>One table's count and digests: <see cref="Sha256"/> over the SQLite rows and <see cref="PostgresSha256"/> over
/// the same keys read back from PostgreSQL, equal once the table is imported.</summary>
public sealed record LegacySqliteTableReport
{
    public required string Name { get; init; }

    public int Rows { get; init; }

    public int Copied { get; init; }

    public int Diverged { get; init; }

    /// <summary>SHA-256 over every SQLite row's canonical content, in key order.</summary>
    public required string Sha256 { get; init; }

    /// <summary>The same digest over the PostgreSQL rows of the same keys; null on a dry run.</summary>
    public string? PostgresSha256 { get; init; }
}
