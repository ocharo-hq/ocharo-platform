using System.Text.Json;
using BrandWorkspace.Application.DesignDocuments.Models;

namespace BrandWorkspace.Api.DesignDocuments.Requests;

/// <summary>Replaces the editor content only when the persisted revision still matches.</summary>
public sealed record SaveDesignDocumentRequest(string? Title, JsonElement Snapshot, int ExpectedRevision)
{
    public DesignDocumentInput ToInput()
    {
        return new DesignDocumentInput(Title, Snapshot);
    }
}
