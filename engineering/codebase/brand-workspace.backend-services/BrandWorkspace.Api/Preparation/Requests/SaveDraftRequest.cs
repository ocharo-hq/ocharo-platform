using BrandWorkspace.Application.Preparation.Models;

namespace BrandWorkspace.Api.Preparation.Requests;

/// <summary>Caller content with an optional revision for the create endpoint.</summary>
public sealed record SaveDraftRequest(Guid BrandId, string Title, string Caption, string Language,
    string Destination, string SourceNotes, Guid[] MediaIds, int? ExpectedRevision = null)
{
    public DraftInput ToInput() => new(BrandId, Title, Caption, Language, Destination, SourceNotes, MediaIds);
}
