namespace BrandWorkspace.Domain.Preparation.Models;

/// <summary>Immutable metadata describing validated original image bytes.</summary>
public sealed record MediaAsset(Guid Id, Guid OrganizationId, string FileName, string ContentType,
    long ByteLength, string Sha256, string Provenance, DateTimeOffset CreatedAtUtc);
