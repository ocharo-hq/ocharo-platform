namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>Stored original bytes ready to serve, or with WoW2 Files the file holding them.</summary>
public sealed record SourcingMediaContent(
    byte[]? Bytes,
    string ContentType,
    string OriginalName,
    string? FileId = null);
