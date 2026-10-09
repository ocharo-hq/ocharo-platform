using System.Security.Cryptography;
using System.Text;

namespace BrandWorkspace.Api.Sso;

/// <summary>Confidential Ocharo client registration; callbacks and organization authority are operator-configured.</summary>
public sealed record DeskSsoClient(string Id, string RedirectUri, string Audience, Guid OrganizationId, string SecretSha256)
{
    private static readonly HashSet<string> Audiences = ["ocharo-catalogue", "ocharo-brand", "ocharo-studio", "ocharo-marketing", "ocharo-editorial", "ocharo-assets"];
    public bool IsValid => Id is { Length: >= 1 and <= 80 } && Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-')
        && Audiences.Contains(Audience) && OrganizationId != Guid.Empty
        && SecretSha256 is { Length: 64 } && SecretSha256.All(Uri.IsHexDigit)
        && Uri.TryCreate(RedirectUri, UriKind.Absolute, out var uri) && uri.Scheme == "https"
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && uri.AbsolutePath == "/api/sso/callback";

    public bool Authenticate(string? secret) => IsValid && secret is { Length: >= 32 and <= 512 }
        && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(secret)), Convert.FromHexString(SecretSha256));
}
