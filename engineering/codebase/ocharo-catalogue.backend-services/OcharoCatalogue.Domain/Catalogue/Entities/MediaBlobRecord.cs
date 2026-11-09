namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>Content-addressed media index: one row per distinct byte sequence.</summary>
public sealed class MediaBlobRecord
{
    public string Sha256 { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long ByteLength { get; set; }
    /// <summary>The WoW2 Files file holding the bytes; none for a file still only in the local blob folder.</summary>
    public string? FileId { get; set; }
    /// <summary>The local blob of a file stored before Files; empty for one stored in Files.</summary>
    public string BlobPath { get; set; } = "";
    public string? PreviewPath { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTimeOffset StoredAt { get; set; }
}
