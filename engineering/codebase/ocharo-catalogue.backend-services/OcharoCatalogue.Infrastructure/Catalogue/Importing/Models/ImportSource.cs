using System.Text.Json.Nodes;

namespace OcharoCatalogue.Infrastructure.Catalogue.Importing.Models;

/// <summary>Represents one workspace product record read for import.</summary>
/// <param name="Json">The parsed record.</param>
/// <param name="Id">The product id, the record's folder name.</param>
/// <param name="RelativePath">The record's path under the source folder, kept as provenance.</param>
/// <param name="Sha256">The record file's SHA-256, kept as provenance.</param>
/// <param name="Directory">The record's folder, which its image paths resolve against.</param>
public sealed record ImportSource(
    JsonObject Json,
    string Id,
    string RelativePath,
    string Sha256,
    string Directory);
