namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents a source record a commerce draft was built from, at its revision and content hash.</summary>
public sealed record SourceEvidence(string Kind, string Id, int Revision, string Sha256);
