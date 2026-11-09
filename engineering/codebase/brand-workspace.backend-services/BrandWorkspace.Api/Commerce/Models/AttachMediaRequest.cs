using BrandWorkspace.Domain.Commerce.Models;

namespace BrandWorkspace.Api.Commerce.Models;

/// <summary>Represents a preparation image to take into a brand, with its provenance.</summary>
public sealed record AttachMediaRequest(int ExpectedRevision, Guid MediaId, MediaApproval Provenance);
