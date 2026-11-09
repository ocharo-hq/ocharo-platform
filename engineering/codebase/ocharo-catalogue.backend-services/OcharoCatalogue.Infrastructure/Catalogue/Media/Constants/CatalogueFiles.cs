namespace OcharoCatalogue.Infrastructure.Catalogue.Media.Constants;

/// <summary>Holds the names Catalogue's files carry in WoW2 Files, the same the migration manifest gives the files it
/// moves.</summary>
public static class CatalogueFiles
{
    /// <summary>
    /// Holds the app every Catalogue file belongs to: Catalogue's Identity client id, so Files applies one policy.
    /// </summary>
    public const string AppId = "catalogue";

    /// <summary>
    /// Holds the record type of the reference that keeps a stored file alive; its record id is the SHA-256.
    /// </summary>
    public const string MediaReference = "catalogue.media";

    /// <summary>Holds the record type of a release artifact's reference, the same the manifest's
    /// <c>catalogue-release-assets</c> gives; its record id is the SHA-256.</summary>
    public const string ReleaseReference = "catalogue.release";
}
