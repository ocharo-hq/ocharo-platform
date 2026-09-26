namespace BrandWorkspace.Application.Preparation.Models;

/// <summary>Caller-authored content; no product facts are generated or inferred.</summary>
public sealed record DraftInput(Guid BrandId, string Title, string Caption, string Language, string Destination,
    string SourceNotes, Guid[] MediaIds);
