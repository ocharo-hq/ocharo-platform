using BrandWorkspace.Domain.Preparation.Models;

namespace BrandWorkspace.Application.Preparation.Models;

/// <summary>An immutable stored image and its metadata.</summary>
public sealed record MediaContent(MediaAsset Asset, byte[] Bytes);

/// <summary>An immutable stored image and the WoW2 Files file holding its bytes; none while PostgreSQL alone holds them.</summary>
public sealed record PreparationMediaFile(MediaAsset Asset, string? FileId);
