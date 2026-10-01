namespace OcharoCatalogue.Infrastructure.Catalogue;

/// <summary>Catalogue host options (section <c>Catalogue</c>).</summary>
public sealed class CatalogueOptions
{
    public const string Section = "Catalogue";

    /// <summary>Runtime data root for blobs; relative paths resolve against the content root.</summary>
    public string DataRoot { get; set; } = "App_Data";

    /// <summary>
    /// Workspace products folder (one <c>{id}/och-{id}.json</c> per product). Read-only source for imports;
    /// the catalogue never writes back to it.
    /// </summary>
    public string? SourceRoot { get; set; }

    /// <summary>Import the source folder automatically when the catalogue is empty on boot.</summary>
    public bool SeedOnEmpty { get; set; } = true;

    /// <summary>Largest accepted image upload.</summary>
    public long MaximumImageBytes { get; set; } = 30 * 1024 * 1024;

    /// <summary>Longest side of generated web previews.</summary>
    public int PreviewSize { get; set; } = 900;
}
