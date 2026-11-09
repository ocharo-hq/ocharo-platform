namespace BrandWorkspace.Api.Sourcing.Models;

/// <summary>Represents one supplier file and its description, read from a multipart upload.</summary>
/// <param name="FileName">The file's original name.</param>
/// <param name="Bytes">The file's bytes.</param>
/// <param name="SupplierId">The supplier the file came from; none puts it in the inbox.</param>
/// <param name="Capture">How the file was captured.</param>
/// <param name="Caption">The caption.</param>
public sealed record SourcingUploadForm(string FileName, byte[] Bytes, Guid? SupplierId, string Capture, string Caption);
