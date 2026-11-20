namespace OcharoEvents.Infrastructure.Events.Services;

/// <summary>Holds which WoW2 Files instance and which read-only tenant API key serve SCFD's stored copies of event images
/// (section <c>Files</c>). Both are optional: with either missing the stored copies are not asked for, and the event image
/// route answers with the image's own source link instead.</summary>
public sealed class FilesServiceOptions
{
    public const string SectionName = "Files";

    /// <summary>Gets or sets the WoW2 Files root URL of SCFD's tenant.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets the tenant API key sent with every Files call: a key of SCFD's tenant that holds
    /// <c>files.read</c> only. A secret, never logged or returned.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets how long the image route waits for Files' signed link before it answers with the source link;
    /// five seconds by default.</summary>
    public TimeSpan LinkTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets whether the stored copies are asked for: an absolute URL and a key, both set.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && Uri.TryCreate(BaseUrl, UriKind.Absolute, out _);
}
