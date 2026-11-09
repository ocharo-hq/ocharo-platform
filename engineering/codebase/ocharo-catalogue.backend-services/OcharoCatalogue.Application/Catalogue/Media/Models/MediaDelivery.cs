namespace OcharoCatalogue.Application.Catalogue.Media.Models;

/// <summary>Represents a stored file a caller may read, and whether anyone may.</summary>
/// <param name="IsPublic">Whether the file belongs to a live approved product or a published release.</param>
/// <param name="Content">Where the bytes come from.</param>
public sealed record MediaDelivery(bool IsPublic, MediaContent Content);
