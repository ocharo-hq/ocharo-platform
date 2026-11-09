namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Whether a price is the owner's confirmed figure or a working draft.</summary>
public static class PricingStatus
{
    public const string Draft = "draft";
    public const string Confirmed = "confirmed";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Draft, Confirmed };
}
