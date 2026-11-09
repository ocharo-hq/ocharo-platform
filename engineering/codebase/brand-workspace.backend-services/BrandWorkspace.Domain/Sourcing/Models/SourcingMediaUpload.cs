namespace BrandWorkspace.Domain.Sourcing.Models;

/// <summary>The outcome of storing one upload: a new item or the existing one with the same bytes.</summary>
public sealed record SourcingMediaUpload(SourcingMedia Media, bool Duplicate, IReadOnlyList<Guid> NearDuplicateIds);
