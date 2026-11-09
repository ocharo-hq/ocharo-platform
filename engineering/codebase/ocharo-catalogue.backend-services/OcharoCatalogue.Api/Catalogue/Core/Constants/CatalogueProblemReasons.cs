namespace OcharoCatalogue.Api.Catalogue.Core.Constants;

/// <summary>Holds the machine reasons the catalogue's HTTP boundary adds to its problem details.</summary>
public static class CatalogueProblemReasons
{
    /// <summary>Holds the reason of a write refused because it came from another origin.</summary>
    public const string CrossOriginWrite = "cross_origin_write";

    /// <summary>Holds the reason of a sign-in request sent over plain HTTP.</summary>
    public const string HttpsRequired = "https_required";

    /// <summary>Holds the reason of a catalogue route refused while the retired SQLite store waits for its import.</summary>
    public const string StorageMoving = "storage_moving";
}
