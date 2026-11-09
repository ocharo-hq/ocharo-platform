using NpgsqlTypes;

namespace OcharoCatalogue.Persistence.Import;

/// <summary>How a column's value travels: its SQLite storage, its PostgreSQL type and its canonical form.</summary>
public enum LegacyColumnKind
{
    Text,
    Integer,
    BigInteger,
    Boolean,
    Uuid,
    Instant,
    Bytes,
}

/// <summary>One column: its SQLite name (EF's PascalCase), its PostgreSQL name, kind and nullability.</summary>
/// <remarks>A nullable column a store made before it existed lacks reads as null (<c>media_blobs.FileId</c> before WoW2
/// Files, <c>catalogue_releases.WithdrawnAtUtc</c> before withdrawal).</remarks>
public sealed record LegacyColumn(string Sqlite, string Postgres, LegacyColumnKind Kind, bool IsNullable = false)
{
    public NpgsqlDbType DbType => Kind switch
    {
        LegacyColumnKind.Text => NpgsqlDbType.Text,
        LegacyColumnKind.Integer => NpgsqlDbType.Integer,
        LegacyColumnKind.BigInteger => NpgsqlDbType.Bigint,
        LegacyColumnKind.Boolean => NpgsqlDbType.Boolean,
        LegacyColumnKind.Uuid => NpgsqlDbType.Uuid,
        LegacyColumnKind.Instant => NpgsqlDbType.TimestampTz,
        LegacyColumnKind.Bytes => NpgsqlDbType.Bytea,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null),
    };
}

/// <summary>One table, the same name in both stores; its first column is its key.</summary>
public sealed record LegacyTable(string Name, IReadOnlyList<LegacyColumn> Columns)
{
    public LegacyColumn Key => Columns[0];
}

/// <summary>Every table of the retired SQLite store, in an order that inserts each row after the rows it keys into.</summary>
public static class LegacySqliteTables
{
    private static LegacyColumn Text(string name, bool nullable = false) => new(name, Column(name), LegacyColumnKind.Text, nullable);

    private static LegacyColumn Int(string name, bool nullable = false) => new(name, Column(name), LegacyColumnKind.Integer, nullable);

    private static LegacyColumn Uuid(string name) => new(name, Column(name), LegacyColumnKind.Uuid);

    private static LegacyColumn Instant(string name, bool nullable = false) => new(name, Column(name), LegacyColumnKind.Instant, nullable);

    private static string Column(string property) => OcharoCatalogueDbContext.ColumnName(property);

    public static readonly IReadOnlyList<LegacyTable> All =
    [
        new("products",
        [
            Text("Id"), Text("GarmentType"), Text("Status"), Int("SortOrder"), Int("Revision"), Instant("CreatedAt"),
            Instant("UpdatedAt"), Text("Json"),
        ]),
        new("product_revisions",
        [
            Uuid("Id"), Text("ProductId"), Int("Revision"), Instant("SavedAt"), Text("Summary"), Text("Json"),
        ]),
        new("collections",
        [
            Text("Id"), Int("SortOrder"), Int("Revision"), Instant("UpdatedAt"), Text("Json"),
        ]),
        new("media_blobs",
        [
            Text("Sha256"), Text("ContentType"), new("ByteLength", "byte_length", LegacyColumnKind.BigInteger),
            Text("FileId", nullable: true), Text("BlobPath"), Text("PreviewPath", nullable: true),
            Int("Width", nullable: true), Int("Height", nullable: true), Instant("StoredAt"),
        ]),
        new("settings",
        [
            Text("Id"), Int("Revision"), Text("Json"),
        ]),
        new("catalogue_designs",
        [
            Uuid("Id"), Text("Title"), Text("Slug"), Text("Locale"), Int("Revision"),
            new("Archived", "archived", LegacyColumnKind.Boolean), Instant("CreatedAtUtc"), Instant("UpdatedAtUtc"),
            Text("Json"),
        ]),
        new("catalogue_design_revisions",
        [
            Uuid("Id"), Uuid("DesignId"), Int("Revision"), Text("Title"), Instant("SavedAtUtc"), Text("Json"),
        ]),
        new("catalogue_releases",
        [
            Uuid("Id"), Uuid("DesignId"), Int("DesignRevision"), Text("Slug"), Text("Locale"), Instant("CreatedAtUtc"),
            Instant("PublishedAtUtc", nullable: true), Instant("WithdrawnAtUtc", nullable: true), Text("Fingerprint"),
            Text("ProductIdsJson"), Text("Json"),
        ]),
        new("catalogue_publications",
        [
            Text("Key"), Uuid("DesignId"), Uuid("ReleaseId"),
        ]),
        new("catalogue_release_assets",
        [
            Text("Sha256"), Text("ContentType"), new("Bytes", "bytes", LegacyColumnKind.Bytes),
        ]),
        new("catalogue_release_files",
        [
            Text("Sha256"), Text("ContentType"), Text("FileId"),
        ]),
    ];
}
