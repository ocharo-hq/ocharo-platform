namespace BrandWorkspace.Api.Commerce.Models;

/// <summary>Represents the metadata of a catalogue edition upload.</summary>
public sealed record CatalogueEditionUpload(int ExpectedRevision, Guid SourceDesignId, int SourceRevision);
