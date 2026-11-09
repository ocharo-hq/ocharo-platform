namespace BrandWorkspace.Api.MediaProcessing.Constants;

/// <summary>
/// Holds the authorization of the media-processing routes: an Identity service token holding <see cref="Scope"/>.
/// </summary>
public static class MediaProcessingPolicy
{
    /// <summary>Holds the policy's name.</summary>
    public const string Name = "MediaProcessing";

    /// <summary>Holds the scope a caller's token must hold; Identity's registry lists it on Desk's resource.</summary>
    public const string Scope = "desk.remove-background";
}
