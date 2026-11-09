namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents a public image with absolute-path URLs and its integrity hash.</summary>
public sealed record PublicImage(
    string Role,
    string Url,
    string? PreviewUrl,
    int? Width,
    int? Height,
    string? Alt,
    string Sha256,
    string? Group = null,
    bool IsActive = true);
