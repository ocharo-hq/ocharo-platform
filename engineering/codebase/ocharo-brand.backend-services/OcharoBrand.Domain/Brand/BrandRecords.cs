namespace OcharoBrand.Domain.Brand;

/// <summary>Single-row draft document (identity + current logos), revision-checked.</summary>
public sealed class DraftRecord
{
    public string Id { get; set; } = "";
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Json { get; set; } = "";
}

/// <summary>Immutable draft snapshot per save.</summary>
public sealed class DraftRevisionRecord
{
    public Guid Id { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Summary { get; set; } = "";
    public string Json { get; set; } = "";
}

public sealed class LogoVersionRecord
{
    public string Id { get; set; } = "";
    public string Slot { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
    public string Json { get; set; } = "";
}

public sealed class ReleaseRecord
{
    public int Version { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string Json { get; set; } = "";
}

public sealed class AppearanceRecord
{
    public string Id { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = "";
    public string Json { get; set; } = "";
}

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

/// <summary>Which app last read the public brand, and which release it saw.</summary>
public sealed class ConsumerRecord
{
    public string App { get; set; } = "";
    public DateTimeOffset LastSeenAt { get; set; }
    public int LastRelease { get; set; }
    public string LastPath { get; set; } = "";
}
