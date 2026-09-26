namespace BrandWorkspace.Domain.Preparation.Models;

/// <summary>The current immutable content snapshot and its review state.</summary>
public sealed record Draft(Guid Id, Guid BrandId, string Title, int Revision, string Status, DateTimeOffset UpdatedAtUtc,
    string Caption, string Language, string Destination, string SourceNotes, IReadOnlyList<MediaAsset> Media,
    int? RequestedReviewRevision, DraftApproval? Approval);
