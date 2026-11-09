namespace OcharoCatalogue.Persistence.Import;

/// <summary>How an import ended: <c>imported</c>, <c>current</c> and <c>planned</c> exit 0, <c>refused</c> exits 1.</summary>
public enum LegacySqliteImportStatus
{
    Imported,
    Current,
    Planned,
    Refused,
}

/// <summary>What one run of the import read, wrote and verified; it carries counts and digests, never rows, ids or
/// keys.</summary>
public sealed record LegacySqliteImportReport
{
    public required LegacySqliteImportStatus Status { get; init; }

    /// <summary>One line on the outcome, for an operator.</summary>
    public required string Summary { get; init; }

    /// <summary>Each copied table in copy order.</summary>
    public IReadOnlyList<LegacyTableReport> Tables { get; init; } = [];

    /// <summary>The file key ring.</summary>
    public LegacyTableReport? Keys { get; init; }

    /// <summary>The WoW2 Files ids the copied media and release-file rows carry.</summary>
    public int FileIds { get; init; }

    /// <summary>The published editions (<c>{slug}:{locale}</c> rows) copied over.</summary>
    public int Publications { get; init; }

    /// <summary>Where the SQLite file was moved once verified, relative to its folder.</summary>
    public string? RetiredTo { get; init; }
}

/// <summary>One table's counts and digests.</summary>
/// <param name="Name">The table, as both stores name it.</param>
/// <param name="Rows">The rows the SQLite store holds.</param>
/// <param name="Copied">The rows this run inserted; on a dry run, the rows it would insert.</param>
/// <param name="Diverged">The rows PostgreSQL holds under the same key with other content.</param>
/// <param name="SqliteSha256">SHA-256 over every SQLite row's canonical digest, in key order.</param>
/// <param name="PostgresSha256">The same digest over PostgreSQL's copies of those rows, read back after the copy; equal
/// to <paramref name="SqliteSha256"/> when nothing diverged, and absent on a plan or a refusal before the copy.</param>
public sealed record LegacyTableReport(
    string Name,
    int Rows,
    int Copied,
    int Diverged,
    string SqliteSha256,
    string? PostgresSha256);
