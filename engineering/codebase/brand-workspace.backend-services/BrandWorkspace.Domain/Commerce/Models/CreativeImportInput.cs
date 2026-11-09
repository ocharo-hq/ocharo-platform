namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents a Studio derivative to record as a creative import.</summary>
public sealed record CreativeImportInput(Guid BrandId, string? ProductId, CreativeSource Source,
    IReadOnlyList<CreativeAssetRelease> AssetReleases, IReadOnlyList<CreativeMaterial> Materials,
    CreativeDerivative Derivative, CreativeSourceApproval? SourceApproval);
