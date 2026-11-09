namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents one artifact of a prepared release.</summary>
/// <param name="Sha256">The SHA-256 of its bytes, which addresses it.</param>
/// <param name="ContentType"><c>application/pdf</c> or <c>image/png</c>.</param>
/// <param name="FileName">The name a download saves it under; Files keeps the name of the first release with these
/// bytes.</param>
/// <param name="Bytes">Its bytes.</param>
public sealed record ReleaseArtifact(
    string Sha256,
    string ContentType,
    string FileName,
    byte[] Bytes);
