namespace OcharoBrand.Infrastructure.Brand;

/// <summary>Stable and immutable media routes.</summary>
public static class BrandUrls
{
    /// <summary>Stable address that always serves the effective logo for a slot (short cache).</summary>
    public static string Logo(string slot) => $"/api/public/brand/logo/{slot}";

    /// <summary>Immutable content address of one exact file (long cache).</summary>
    public static string Media(string sha256) => $"/api/public/brand/media/{sha256}";
}
