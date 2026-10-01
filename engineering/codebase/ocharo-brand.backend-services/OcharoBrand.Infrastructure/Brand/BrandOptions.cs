namespace OcharoBrand.Infrastructure.Brand;

/// <summary>Brand host options (section <c>Brand</c>).</summary>
public sealed class BrandOptions
{
    public const string Section = "Brand";

    /// <summary>Runtime data root for blobs; relative paths resolve against the content root.</summary>
    public string DataRoot { get; set; } = "App_Data";

    /// <summary>Folder with the approved logo files (read-only seed input; hashes recorded as provenance).</summary>
    public string? SeedLogoRoot { get; set; }

    public long MaximumImageBytes { get; set; } = 15 * 1024 * 1024;
    public int PreviewSize { get; set; } = 1200;
}
