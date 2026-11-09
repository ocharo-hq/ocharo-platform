namespace BrandWorkspace.Api.Preparation.Models;

/// <summary>Applies a review transition only to the specified current revision.</summary>
public sealed record RevisionRequest(int ExpectedRevision, string? Reason = null);
