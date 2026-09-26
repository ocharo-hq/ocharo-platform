using BrandWorkspace.Domain.Preparation.Models;

namespace BrandWorkspace.Application.Preparation.Models;

/// <summary>The stored revision contract; media metadata is frozen with ordered immutable identities.</summary>
public sealed record DraftContent(Guid BrandId, string Title, string Caption, string Language, string Destination,
    string SourceNotes, IReadOnlyList<MediaAsset> Media);
