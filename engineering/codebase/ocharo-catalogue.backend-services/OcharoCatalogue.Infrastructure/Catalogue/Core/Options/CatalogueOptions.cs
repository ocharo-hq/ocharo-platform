namespace OcharoCatalogue.Infrastructure.Catalogue.Core.Options;

/// <summary>Holds the catalogue host options (section <c>Catalogue</c>).</summary>
public sealed class CatalogueOptions
{
    /// <summary>Holds the configuration section the options bind from.</summary>
    public const string Section = "Catalogue";

    /// <summary>
    /// Gets or sets the runtime data root for blobs; relative paths resolve against the content root.
    /// </summary>
    public string DataRoot { get; set; } = "App_Data";

    /// <summary>
    /// Gets or sets the workspace products folder (one <c>{id}/och-{id}.json</c> per product), the read-only source for
    /// imports; the catalogue never writes back to it.
    /// </summary>
    public string? SourceRoot { get; set; }

    /// <summary>
    /// Gets or sets whether the source folder is imported automatically when the catalogue is empty on boot.
    /// </summary>
    public bool SeedOnEmpty { get; set; } = true;

    /// <summary>Gets or sets the largest accepted image upload.</summary>
    public long MaximumImageBytes { get; set; } = 30 * 1024 * 1024;

    /// <summary>Gets or sets the longest side of generated web previews.</summary>
    public int PreviewSize { get; set; } = 900;

    /// <summary>
    /// Gets or sets the HTTPS origins (scheme and authority, such as <c>https://localhost:8282</c>) whose browser
    /// writes
    /// the API accepts besides its own; every other cross-origin write is refused. Empty by default.
    /// </summary>
    public string[] TrustedOrigins { get; set; } = [];
}
