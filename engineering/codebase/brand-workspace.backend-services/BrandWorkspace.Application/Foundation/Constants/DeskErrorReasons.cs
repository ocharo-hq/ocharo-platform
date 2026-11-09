namespace BrandWorkspace.Application.Foundation.Constants;

/// <summary>Holds the reasons a Desk error names in its metadata where its kind alone does not decide the HTTP status.</summary>
public static class DeskErrorReasons
{
    /// <summary>Holds the metadata key that carries the reason.</summary>
    public const string Key = "reason";

    /// <summary>Holds the reason of a body or file over its size limit.</summary>
    public const string TooLarge = "too-large";

    /// <summary>Holds the reason of a body whose content type the route does not accept.</summary>
    public const string UnsupportedMediaType = "unsupported-media-type";

    /// <summary>Holds the reason of a call WoW2 Files refused.</summary>
    public const string Upstream = "upstream";
}
