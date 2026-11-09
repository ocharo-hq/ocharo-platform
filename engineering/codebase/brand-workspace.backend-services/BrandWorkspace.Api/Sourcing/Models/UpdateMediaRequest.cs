namespace BrandWorkspace.Api.Sourcing.Models;

/// <summary>Represents a media item's replacement supplier, capture and caption at the revision the operator edited.</summary>
public sealed record UpdateMediaRequest(int ExpectedRevision, Guid? SupplierId, string? Capture, string? Caption);
