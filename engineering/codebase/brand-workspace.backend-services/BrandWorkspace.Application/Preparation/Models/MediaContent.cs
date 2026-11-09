using BrandWorkspace.Domain.Preparation.Models;

namespace BrandWorkspace.Application.Preparation.Models;

/// <summary>An immutable stored image and its metadata.</summary>
public sealed record MediaContent(MediaAsset Asset, byte[] Bytes);
