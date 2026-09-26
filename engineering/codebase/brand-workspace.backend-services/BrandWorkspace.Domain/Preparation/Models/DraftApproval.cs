namespace BrandWorkspace.Domain.Preparation.Models;

/// <summary>An exact revision approval by an authenticated organization owner.</summary>
public sealed record DraftApproval(Guid Id, int Revision, DateTimeOffset ApprovedAtUtc, string ApprovedBy);
