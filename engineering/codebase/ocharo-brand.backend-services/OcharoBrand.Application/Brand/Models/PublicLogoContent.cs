using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Application.Brand.Models;

/// <summary>Represents the file a stable logo address serves right now.</summary>
/// <param name="Logo">The effective logo version: an active appearance's override, else the released file.</param>
/// <param name="Media">Its bytes or Files link.</param>
/// <param name="Release">The latest release's version, which the reading app is recorded against.</param>
public sealed record PublicLogoContent(LogoVersion Logo, MediaContent Media, int Release);
