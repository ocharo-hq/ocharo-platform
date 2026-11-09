namespace BrandWorkspace.Domain.DesignDocuments.Models;

/// <summary>Represents the outputs a catalogue design produces: a web version, a PDF or both.</summary>
public sealed record CatalogueOutputs(bool Web, bool Pdf);
