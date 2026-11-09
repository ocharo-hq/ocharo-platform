namespace OcharoBrand.Domain.Brand;

/// <summary>An uploaded logo file for one slot; bytes live in the content-addressed media store.</summary>
public sealed record LogoVersion(
    string Id,
    string Slot,
    string Sha256,
    string FileName,
    string ContentType,
    long ByteLength,
    int? Width,
    int? Height,
    bool HasPreview,
    string Status,
    string? Note,
    string? SourcePath,
    DateTimeOffset UploadedAt);
