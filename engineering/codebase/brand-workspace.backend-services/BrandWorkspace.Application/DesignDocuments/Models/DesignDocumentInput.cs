using System.Text.Json;

namespace BrandWorkspace.Application.DesignDocuments.Models;

/// <summary>The editable content of a design document, without caller or organization authority.</summary>
public sealed record DesignDocumentInput(string? Title, JsonElement Snapshot);
