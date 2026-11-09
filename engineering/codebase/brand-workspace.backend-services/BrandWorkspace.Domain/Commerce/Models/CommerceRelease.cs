namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents a published commerce release: a draft revision frozen with its note.</summary>
public sealed record CommerceRelease(
    Guid Id,
    Guid BrandId,
    int Revision,
    int SourceRevision,
    DateTimeOffset PublishedAt,
    string Note,
    CommerceSnapshot Snapshot,
    DateTimeOffset? WithdrawnAt = null);
