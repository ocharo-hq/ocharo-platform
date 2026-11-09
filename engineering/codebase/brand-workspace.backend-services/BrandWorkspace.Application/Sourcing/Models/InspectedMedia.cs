namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>Original bytes with the facts derived from them on the server.</summary>
public sealed record InspectedMedia(
    byte[] Bytes,
    string Sha256,
    string ContentType,
    int? Width,
    int? Height,
    byte[]? Thumbnail,
    DateTimeOffset? TakenAt,
    bool HasLocation,
    long? PerceptualHash);
