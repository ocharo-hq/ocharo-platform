namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>Current named catalogue design; saves retain immutable revisions.</summary>
public sealed class CatalogueDesignRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Locale { get; set; } = "";
    public int Revision { get; set; }
    public bool Archived { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string Json { get; set; } = "";
}
public sealed class CatalogueDesignRevisionRecord
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public int Revision { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset SavedAtUtc { get; set; }
    public string Json { get; set; } = "";
}
/// <summary>The payload and source fingerprint never change after preparation.</summary>
public sealed class CatalogueReleaseRecord
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public int DesignRevision { get; set; }
    public string Slug { get; set; } = "";
    public string Locale { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset? WithdrawnAtUtc { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ProductIdsJson { get; set; } = "[]";
    public string Json { get; set; } = "";
}
/// <summary>Mutable pointer to one prepared release; publishing never rewrites released bytes.</summary>
public sealed class CataloguePublicationRecord
{
    public string Key { get; set; } = "";
    public Guid DesignId { get; set; }
    public Guid ReleaseId { get; set; }
}
/// <summary>A release artifact whose bytes SQLite holds: every artifact a run without WoW2 Files prepared, and those
/// prepared before Files until the migration moves them.</summary>
public sealed class CatalogueReleaseAssetRecord
{
    public string Sha256 { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Bytes { get; set; } = [];
}

/// <summary>A release artifact held in WoW2 Files, by SHA-256: prepared there, or moved there and filled at boot.</summary>
public sealed class CatalogueReleaseFileRecord
{
    public string Sha256 { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string FileId { get; set; } = "";
}
