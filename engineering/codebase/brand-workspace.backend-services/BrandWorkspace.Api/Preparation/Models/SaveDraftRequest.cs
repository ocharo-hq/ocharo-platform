using BrandWorkspace.Application.Preparation.Models;

namespace BrandWorkspace.Api.Preparation.Models;

/// <summary>Caller content with an optional revision for the create endpoint.</summary>
public sealed record SaveDraftRequest(Guid BrandId, string Title, string Caption, string Language,
    string Destination, string SourceNotes, Guid[] MediaIds, int? ExpectedRevision = null)
{
    /// <summary>Maps the request to the draft's content.</summary>
    public DraftInput ToInput()
    {
        return new DraftInput(BrandId, Title, Caption, Language, Destination, SourceNotes, MediaIds);
    }
}
