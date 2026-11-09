using BrandWorkspace.Application.Files.Models;

namespace BrandWorkspace.Application.Commerce.Models;

/// <summary>Represents a released edition artifact and the name a download saves it under.</summary>
/// <param name="Content">The artifact: a WoW2 Files link, or its bytes.</param>
/// <param name="FileName">The name a download saves it under; none when it opens inline.</param>
public sealed record PublicAsset(StoredContent Content, string? FileName);
