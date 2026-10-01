namespace BrandWorkspace.Domain.DesignDocuments.Models;

/// <summary>Identifies an immutable document revision and its recorded author.</summary>
public sealed record DesignDocumentRevision(
    int Revision,
    string Title,
    bool Archived,
    string SavedBy,
    DateTimeOffset SavedAtUtc);
