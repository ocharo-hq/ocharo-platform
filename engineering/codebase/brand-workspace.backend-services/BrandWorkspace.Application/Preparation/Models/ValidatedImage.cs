namespace BrandWorkspace.Application.Preparation.Models;

/// <summary>Validated original image bytes and a digest over those exact bytes.</summary>
public sealed record ValidatedImage(byte[] Bytes, string ContentType, string Sha256);
