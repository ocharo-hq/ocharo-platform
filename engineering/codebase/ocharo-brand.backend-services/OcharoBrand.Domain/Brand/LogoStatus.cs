namespace OcharoBrand.Domain.Brand;

public static class LogoStatus
{
    public const string Draft = "draft";
    public const string Approved = "approved";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Draft, Approved };
}
