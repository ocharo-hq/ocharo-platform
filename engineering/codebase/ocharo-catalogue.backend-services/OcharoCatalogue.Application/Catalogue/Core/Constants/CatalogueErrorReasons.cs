namespace OcharoCatalogue.Application.Catalogue.Core.Constants;

/// <summary>
/// Holds the reasons a catalogue error carries in its metadata, for a delivery boundary to refine its status.
/// </summary>
public static class CatalogueErrorReasons
{
    /// <summary>Holds the metadata key a reason sits under.</summary>
    public const string Key = "catalogueReason";

    /// <summary>Holds the reason of an upload larger than the catalogue accepts.</summary>
    public const string PayloadTooLarge = "payloadTooLarge";

    /// <summary>Holds the reason of an upload in a format the catalogue does not accept.</summary>
    public const string UnsupportedMediaType = "unsupportedMediaType";

    /// <summary>Holds the reason of a call WoW2 Files refused.</summary>
    public const string UpstreamRefused = "upstreamRefused";
}
