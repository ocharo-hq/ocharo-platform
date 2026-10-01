namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>Current product row: indexed scalars plus the serialized <c>ProductDocument</c>.</summary>
public sealed class ProductRecord
{
    public string Id { get; set; } = "";
    public string GarmentType { get; set; } = "";
    public string Status { get; set; } = "";
    public int SortOrder { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Json { get; set; } = "";
}

/// <summary>An immutable snapshot of one saved product revision.</summary>
public sealed class ProductRevisionRecord
{
    public Guid Id { get; set; }
    public string ProductId { get; set; } = "";
    public int Revision { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Summary { get; set; } = "";
    public string Json { get; set; } = "";
}

/// <summary>Current collection row.</summary>
public sealed class CollectionRecord
{
    public string Id { get; set; } = "";
    public int SortOrder { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Json { get; set; } = "";
}

/// <summary>Content-addressed media index: one row per distinct byte sequence.</summary>
public sealed class MediaBlobRecord
{
    public string Sha256 { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long ByteLength { get; set; }
    public string BlobPath { get; set; } = "";
    public string? PreviewPath { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTimeOffset StoredAt { get; set; }
}

/// <summary>Single-row settings document.</summary>
public sealed class SettingsRecord
{
    public string Id { get; set; } = "";
    public int Revision { get; set; }
    public string Json { get; set; } = "";
}
