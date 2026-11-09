namespace OcharoBrand.Application.Brand.Models;

/// <summary>A logo slot: <see cref="Url"/> is stable (follows changes), <see cref="FileUrl"/> is this exact file.
/// <see cref="FileId"/> names the WoW2 Files file holding it, so another Ocharo app references it instead of copying its
/// bytes (research/files-adoption.md #11); none while the file is still only in the local blob folder.</summary>
public sealed record PublicLogo(string Url, string FileUrl, string? PreviewUrl, string ContentType, int? Width, int? Height, string Sha256, bool FromAppearance,
    string? FileId);
