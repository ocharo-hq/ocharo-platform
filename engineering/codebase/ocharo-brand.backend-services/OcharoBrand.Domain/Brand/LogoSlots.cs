namespace OcharoBrand.Domain.Brand;

/// <summary>Logo slots every Ocharo app can ask for.</summary>
public static class LogoSlots
{
    public const string Master = "master";
    public const string OnLight = "on-light";
    public const string OnDark = "on-dark";
    public const string Avatar = "avatar";
    public const string Favicon = "favicon";
    public const string Social = "social";

    public static readonly IReadOnlyList<string> All = [Master, OnLight, OnDark, Avatar, Favicon, Social];
}
