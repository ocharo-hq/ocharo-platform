namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents commerce read from a preserved brand: its snapshot and media.</summary>
public sealed record ImportedCommerce(CommerceSnapshot Snapshot, IReadOnlyList<CommerceBlob> Blobs);
