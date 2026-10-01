using Microsoft.EntityFrameworkCore;
namespace OcharoCatalogue.Persistence.Catalogue;

/// <summary>Adds designer tables to existing pilot stores. Existing product tables and rows are untouched.</summary>
public static class CatalogueDesignSchema
{
    public static async Task EnsureAsync(OcharoCatalogueDbContext db, CancellationToken ct=default)
    {
        string[] statements=[
            "CREATE TABLE IF NOT EXISTS catalogue_designs (Id TEXT NOT NULL PRIMARY KEY, Title TEXT NOT NULL, Slug TEXT NOT NULL, Locale TEXT NOT NULL, Revision INTEGER NOT NULL, Archived INTEGER NOT NULL, CreatedAtUtc INTEGER NOT NULL, UpdatedAtUtc INTEGER NOT NULL, Json TEXT NOT NULL)",
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_catalogue_designs_Slug_Locale ON catalogue_designs(Slug,Locale) WHERE Archived = 0",
            "CREATE TABLE IF NOT EXISTS catalogue_design_revisions (Id TEXT NOT NULL PRIMARY KEY, DesignId TEXT NOT NULL, Revision INTEGER NOT NULL, Title TEXT NOT NULL, SavedAtUtc INTEGER NOT NULL, Json TEXT NOT NULL)",
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_catalogue_design_revisions_DesignId_Revision ON catalogue_design_revisions(DesignId,Revision)",
            "CREATE TABLE IF NOT EXISTS catalogue_releases (Id TEXT NOT NULL PRIMARY KEY, DesignId TEXT NOT NULL, DesignRevision INTEGER NOT NULL, Slug TEXT NOT NULL, Locale TEXT NOT NULL, CreatedAtUtc INTEGER NOT NULL, PublishedAtUtc INTEGER NULL, WithdrawnAtUtc INTEGER NULL, Fingerprint TEXT NOT NULL, ProductIdsJson TEXT NOT NULL, Json TEXT NOT NULL)",
            "CREATE INDEX IF NOT EXISTS IX_catalogue_releases_DesignId_CreatedAtUtc ON catalogue_releases(DesignId,CreatedAtUtc)",
            "CREATE TABLE IF NOT EXISTS catalogue_publications (Key TEXT NOT NULL PRIMARY KEY, DesignId TEXT NOT NULL, ReleaseId TEXT NOT NULL)",
            "CREATE TABLE IF NOT EXISTS catalogue_release_assets (Sha256 TEXT NOT NULL PRIMARY KEY, ContentType TEXT NOT NULL, Bytes BLOB NOT NULL)"
        ];
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        foreach(var sql in statements)await db.Database.ExecuteSqlRawAsync(sql,ct);
        if(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('catalogue_releases') WHERE name = 'WithdrawnAtUtc'").SingleAsync(ct)==0)
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE catalogue_releases ADD COLUMN WithdrawnAtUtc INTEGER NULL",ct);
        await tx.CommitAsync(ct);
    }
}
