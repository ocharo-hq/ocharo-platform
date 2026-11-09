namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>A stored 480 px WebP thumbnail, or with WoW2 Files the original whose variant serves as one.</summary>
public sealed record SourcingThumbnail(byte[]? Bytes, string? FileId);
