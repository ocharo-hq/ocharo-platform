using System.Text.Json;
using BrandWorkspace.Application.DesignDocuments.Models;

namespace BrandWorkspace.Api.DesignDocuments.Models;

/// <summary>Creates an editor document within the route's authenticated organization.</summary>
public sealed record CreateDesignDocumentRequest(string? Kind, string? Title, JsonElement Snapshot)
{
    /// <summary>Maps the request to the document's title and snapshot.</summary>
    public DesignDocumentInput ToInput()
    {
        return new DesignDocumentInput(Title, Snapshot);
    }
}
