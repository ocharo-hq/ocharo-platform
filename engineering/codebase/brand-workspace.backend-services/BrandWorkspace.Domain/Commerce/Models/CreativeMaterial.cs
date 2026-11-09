namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents the material filling one slot of a creative derivative.</summary>
public sealed record CreativeMaterial(string Slot, string Source, string? MaterialId, string? Sha256);
