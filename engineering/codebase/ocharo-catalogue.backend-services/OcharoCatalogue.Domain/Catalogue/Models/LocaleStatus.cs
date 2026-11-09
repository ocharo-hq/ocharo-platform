namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Copy approval for one language.</summary>
public static class LocaleStatus
{
    public const string Draft = "draft";
    public const string Approved = "approved";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Draft, Approved };
}
