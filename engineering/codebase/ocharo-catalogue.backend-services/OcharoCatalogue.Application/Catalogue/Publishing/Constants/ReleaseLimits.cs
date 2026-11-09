namespace OcharoCatalogue.Application.Catalogue.Publishing.Constants;

/// <summary>Holds the size limits of a prepared release's artifacts.</summary>
public static class ReleaseLimits
{
    /// <summary>Holds the most bytes one release's artifacts may hold together: 256 MiB.</summary>
    public const int MaximumTotalBytes = 256 * 1024 * 1024;

    /// <summary>Holds the most bytes one artifact may hold: 64 MiB.</summary>
    public const int MaximumFileBytes = 64 * 1024 * 1024;

    /// <summary>Holds the most files one preparation may upload.</summary>
    public const int MaximumFiles = 201;

    /// <summary>Holds the longest preparation metadata a form may carry.</summary>
    public const int MaximumMetadataLength = 1000;
}
