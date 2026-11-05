namespace BrandWorkspace.Api.Access;

/// <summary>Holds the <c>Identity:Resource</c> section: Desk as a resource of Ocharo's Identity instance.</summary>
public sealed record IdentityResourceSettings
{
    public const string SectionName = "Identity:Resource";

    /// <summary>The route that hears Identity's revocation signals for this resource.</summary>
    public const string SignalPath = "/signals/identity";

    /// <summary>The URI callers name as <c>resource</c>; the descriptor's site link renders it as a bare origin. Required.</summary>
    public string? Uri { get; init; }

    /// <summary>This resource's <c>SignalUrl</c> at the instance, <c>{site}/signals/identity</c>: the <c>aud</c> a signal must name.
    /// Unset maps no route, and the deny list then stays empty.</summary>
    public string? SignalUrl { get; init; }

    /// <summary>Returns <paramref name="value"/> as Identity writes it into <c>aud</c>, <see cref="System.Uri.AbsoluteUri"/>, or null
    /// unless it passes the rule Identity holds its registry to: absolute HTTPS (HTTP only on a loopback host) without a fragment.</summary>
    public static string? Normalize(string? value) =>
        System.Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == System.Uri.UriSchemeHttps || (uri.Scheme == System.Uri.UriSchemeHttp && uri.IsLoopback))
        && uri.Fragment.Length == 0
            ? uri.AbsoluteUri
            : null;
}
