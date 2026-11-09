namespace BrandWorkspace.Api.Commerce.Models;

/// <summary>Represents a publication at the draft revision the owner saw, with its reason.</summary>
public sealed record ReleaseRequest(int ExpectedRevision, string Note);
