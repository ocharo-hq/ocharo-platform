using BrandWorkspace.Domain.Commerce;

namespace BrandWorkspace.Application.Commerce.Models;

/// <summary>Represents a brand's commerce draft, or none before its first save.</summary>
/// <param name="Draft">The current draft, if any.</param>
public sealed record CommerceDraftLookup(CommerceDraft? Draft);
