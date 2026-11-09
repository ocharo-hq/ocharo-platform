namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Review question state.</summary>
public static class IssueStatus
{
    public const string Open = "open";
    public const string Resolved = "resolved";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Open, Resolved };
}
