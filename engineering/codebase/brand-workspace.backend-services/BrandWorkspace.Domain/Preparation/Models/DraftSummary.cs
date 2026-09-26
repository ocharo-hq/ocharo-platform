namespace BrandWorkspace.Domain.Preparation.Models;

/// <summary>The current editable head, with status scoped to its revision.</summary>
public sealed record DraftSummary(Guid Id, Guid BrandId, string Title, int Revision, string Status, DateTimeOffset UpdatedAtUtc);
