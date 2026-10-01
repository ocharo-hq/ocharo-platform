using System.Text.Json;
using BrandWorkspace.Application.DesignDocuments.Models;

namespace BrandWorkspace.Api.DesignDocuments.Requests;

/// <summary>Creates an editor document within the route's authenticated organization.</summary>
public sealed record CreateDesignDocumentRequest(string? Kind, string? Title, JsonElement Snapshot)
{
    public DesignDocumentInput ToInput()
    {
        return new DesignDocumentInput(Title, Snapshot);
    }
}
