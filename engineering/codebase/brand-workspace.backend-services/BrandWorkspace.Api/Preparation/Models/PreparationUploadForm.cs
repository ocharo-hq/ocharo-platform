namespace BrandWorkspace.Api.Preparation.Models;

/// <summary>Represents one preparation image and its provenance, read from a multipart upload.</summary>
/// <param name="FileName">The image's original name.</param>
/// <param name="Provenance">The image's provenance.</param>
/// <param name="Bytes">The image's bytes.</param>
public sealed record PreparationUploadForm(string FileName, string Provenance, byte[] Bytes);
