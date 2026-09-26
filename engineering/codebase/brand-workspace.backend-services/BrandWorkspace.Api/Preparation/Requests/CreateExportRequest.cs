namespace BrandWorkspace.Api.Preparation.Requests;

/// <summary>Prepares one approved revision with an organization-scoped retry identity.</summary>
public sealed record CreateExportRequest(int ExpectedRevision, Guid IdempotencyKey);
