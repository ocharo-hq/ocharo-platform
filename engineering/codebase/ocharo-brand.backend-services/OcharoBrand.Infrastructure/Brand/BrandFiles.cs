namespace OcharoBrand.Infrastructure.Brand;

/// <summary>The names Brand's files carry in WoW2 Files, the same the migration manifest gives the files it moves.</summary>
public static class BrandFiles
{
    /// <summary>The app every Brand file belongs to: Brand's Identity client id, so Files applies one policy.</summary>
    public const string AppId = "brand";

    /// <summary>The record type of the reference that keeps a stored file alive; its record id is the SHA-256.</summary>
    public const string MediaReference = "brand.media";
}
