namespace BrandWorkspace.Api.Commerce.Models;

/// <summary>Represents a catalogue edition upload's metadata and artifacts by form field.</summary>
public sealed record CatalogueEditionForm(CatalogueEditionUpload Metadata, IReadOnlyDictionary<string, byte[]> Files);
