namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents a product-owned image group; its category switches matching sets across a catalogue.</summary>
public sealed record ProductMediaGroup(string Id, string Label, string Category, IReadOnlyList<string> MediaIds);
