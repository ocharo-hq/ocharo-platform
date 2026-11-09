namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents the publication approval and provenance of one brand media item.</summary>
public sealed record MediaApproval(
    string Kind,
    bool ApprovedForPublication,
    string? SourceUrl = null,
    string? Credit = null,
    string? Rights = null);
