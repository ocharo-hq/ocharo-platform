using System.Text.Json;
using BrandWorkspace.Application.DesignDocuments.Models;

namespace BrandWorkspace.Api.DesignDocuments.Models;

/// <summary>Replaces the editor content only when the persisted revision still matches.</summary>
public sealed record SaveDesignDocumentRequest(string? Title, JsonElement Snapshot, int ExpectedRevision)
{
    /// <summary>Maps the request to the document's title and snapshot.</summary>
    public DesignDocumentInput ToInput()
    {
        return new DesignDocumentInput(Title, Snapshot);
    }
}
