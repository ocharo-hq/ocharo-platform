namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>An image attached to a product; bytes live in the content-addressed media store.</summary>
public sealed record ProductMedia(
    string Id,
    string Role,
    string FileName,
    string ContentType,
    long ByteLength,
    string Sha256,
    int? Width,
    int? Height,
    bool HasPreview,
    string? Alt,
    string? SourcePath,
    bool IsActive = true,
    string? Group = null);
