namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents the design revision and fingerprint a release was rendered against.</summary>
public sealed record ReleasePrepareMetadata(int ExpectedRevision, string? Fingerprint);
