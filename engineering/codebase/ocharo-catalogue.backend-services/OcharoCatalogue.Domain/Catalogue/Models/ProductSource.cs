namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Import provenance: the file and hash a record was read from.</summary>
public sealed record ProductSource(string Kind, string? Path, string? Sha256, DateTimeOffset? ImportedAt);
