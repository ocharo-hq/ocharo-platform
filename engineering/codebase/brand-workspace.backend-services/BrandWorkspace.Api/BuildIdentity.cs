namespace BrandWorkspace.Api;

/// <summary>Immutable assembly identity assigned by CI; local builds report dev.</summary>
public static class BuildIdentity
{
    /// <summary>Migration history stores the product version, excluding commit build metadata.</summary>
    public static string ProductVersion => ProductVersionFrom(Version);

    /// <summary>Preserves prerelease identity; only informational metadata follows '+'.</summary>
    public static string ProductVersionFrom(string informationalVersion) => informationalVersion.Split('+', 2)[0];

    public static string Version => typeof(BuildIdentity).Assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "dev";
}
