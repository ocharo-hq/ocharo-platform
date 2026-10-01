namespace BrandWorkspace.Domain.DesignDocuments.Models;

/// <summary>Lists saved documents without transferring editor snapshots.</summary>
public sealed record DesignDocumentSummary(
    Guid Id,
    Guid OrganizationId,
    string Kind,
    string Title,
    int Revision,
    bool Archived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
