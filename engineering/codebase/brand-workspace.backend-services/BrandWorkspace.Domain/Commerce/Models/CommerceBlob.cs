namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>
/// Represents content-addressed brand media: its bytes, or with WoW2 Files the file holding them and no bytes.
/// </summary>
public sealed record CommerceBlob(string Sha256, string ContentType, byte[] Bytes, string? FileId = null);
