using System.Text.Json;

namespace BrandWorkspace.Domain.DesignDocuments.Models;

/// <summary>An organization-owned editor document at an immutable saved revision.</summary>
public sealed record DesignDocument(
    Guid Id,
    Guid OrganizationId,
    string Kind,
    string Title,
    JsonElement Snapshot,
    int Revision,
    bool Archived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
