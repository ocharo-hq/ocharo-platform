using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Application.Brand.Models;

/// <summary>Where one stored file's bytes come from: a short-lived WoW2 Files link to redirect to
/// (<paramref name="Location"/>), or a stream from the local blob folder (<paramref name="Content"/>).</summary>
/// <param name="Record">The stored file.</param>
/// <param name="ContentType">The type the bytes carry: the original's, or WebP for a preview.</param>
/// <param name="Location">The Files link, valid for <see cref="MediaContent.LinkLifetime"/>; none for local bytes.</param>
/// <param name="Content">The local bytes; none for a Files link.</param>
public sealed record MediaContent(MediaBlobRecord Record, string ContentType, Uri? Location, Stream? Content)
{
    /// <summary>How long a Files link stays valid; a cached redirect must expire before it.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    /// <summary>The longest a response that redirects to a Files link may be cached, in seconds.</summary>
    public const int RedirectMaxAgeSeconds = 300;
}
