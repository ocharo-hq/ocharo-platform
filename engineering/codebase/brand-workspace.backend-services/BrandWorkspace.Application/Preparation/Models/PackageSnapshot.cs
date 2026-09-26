using BrandWorkspace.Domain.Preparation.Models;

namespace BrandWorkspace.Application.Preparation.Models;

/// <summary>The complete immutable input for one manually delivered ZIP.</summary>
public sealed record PackageSnapshot(Guid OrganizationId, ManualExport Export, Draft Draft, IReadOnlyList<MediaContent> Media);
