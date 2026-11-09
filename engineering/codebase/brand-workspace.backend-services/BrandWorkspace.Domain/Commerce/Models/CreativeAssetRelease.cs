namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents a released asset a creative derivative used, by its content hash.</summary>
public sealed record CreativeAssetRelease(string Id, string Sha256);
