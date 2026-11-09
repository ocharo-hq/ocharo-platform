namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents a brand's saved commerce draft at a revision.</summary>
public sealed record CommerceDraft(Guid BrandId, int Revision, CommerceSnapshot Snapshot, DateTimeOffset UpdatedAt);
