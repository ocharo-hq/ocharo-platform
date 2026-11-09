namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents one artifact file uploaded with a preparation.</summary>
/// <param name="FileName">The uploaded file's name, such as <c>page-cover.png</c>.</param>
/// <param name="Bytes">The uploaded bytes.</param>
public sealed record ReleaseUpload(string FileName, byte[] Bytes);
