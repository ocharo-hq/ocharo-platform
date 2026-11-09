using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Application.Brand.Models;

/// <summary>Represents the editable draft with its logo versions and what the next release would change.</summary>
public sealed record DraftView(BrandDraft Draft, int Revision, DateTimeOffset UpdatedAt, IReadOnlyList<LogoVersion> Logos, int? LatestRelease, IReadOnlyList<string> UnpublishedChanges);
