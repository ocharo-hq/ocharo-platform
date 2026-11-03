using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace OcharoCatalogue.Tests;

/// <summary>
/// What the Catalogue test hosts replace in sign-in: a stand-in for the Identity BFF cookie scheme, where a request that
/// names a known person in <see cref="PersonHeader"/> is signed in with the claims Identity's token carries and any other
/// request is anonymous; no call reaches the Identity instance. The persons' roles grant what Ocharo's Identity grants
/// Catalogue's client: owner edits and approves, editor edits, viewer reads.
/// </summary>
public static class CatalogueSignIn
{
    /// <summary>Names the person a request signs in as.</summary>
    public const string PersonHeader = "X-Test-Person";

    /// <summary>The organization the test catalogue belongs to (<c>Catalogue:OrganizationId</c>).</summary>
    public static readonly Guid OrganizationId = Guid.Parse("6f1d3c2a-8b4e-4f7a-9c1d-2e3f4a5b6c7d");

    /// <summary>Another organization, whose members have no access to this catalogue.</summary>
    public static readonly Guid OtherOrganizationId = Guid.Parse("0a9b8c7d-6e5f-4a3b-8c2d-1e0f9a8b7c6d");

    public const string Owner = "owner";
    public const string Editor = "editor";
    public const string Viewer = "viewer";
    /// <summary>An owner of <see cref="OtherOrganizationId"/>.</summary>
    public const string Outsider = "outsider";
    /// <summary>A person signed in without any organization.</summary>
    public const string Unaffiliated = "unaffiliated";

    /// <summary>Settings that satisfy the Catalogue's sign-in validation without any network call to the authority.</summary>
    public static IReadOnlyDictionary<string, string?> Settings { get; } = new Dictionary<string, string?>
    {
        ["Identity:Client:Authority"] = "https://identity.example.test/",
        ["Identity:Client:ClientId"] = "catalogue",
        ["Catalogue:OrganizationId"] = OrganizationId.ToString("D"),
    };

    private static readonly Dictionary<string, (Guid? Organization, string? Role)> People = new(StringComparer.Ordinal)
    {
        [Owner] = (OrganizationId, "owner"),
        [Editor] = (OrganizationId, "editor"),
        [Viewer] = (OrganizationId, "viewer"),
        [Outsider] = (OtherOrganizationId, "owner"),
        [Unaffiliated] = (null, null),
    };

    /// <summary>Adds the sign-in settings and replaces the BFF cookie handler with the header-driven test handler.</summary>
    public static IWebHostBuilder UseCatalogueSignIn(this IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(Settings));
        builder.ConfigureServices(services => services.PostConfigure<AuthenticationOptions>(options =>
        {
            foreach (var scheme in options.Schemes.Where(scheme => scheme.Name == BffAuthenticationSchemeConstants.Cookie))
                scheme.HandlerType = typeof(Handler);
        }));
        return builder;
    }

    /// <summary>A client of <paramref name="factory"/> that signs in as <paramref name="person"/>; null stays anonymous.</summary>
    public static HttpClient CreateClient(WebApplicationFactory<Program> factory, string? person, string baseAddress = "https://localhost")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri(baseAddress), AllowAutoRedirect = false, HandleCookies = false });
        if (person is not null) client.DefaultRequestHeaders.Add(PersonHeader, person);
        return client;
    }

    private static string[] Permissions(string role) => role switch
    {
        "owner" or "admin" => ["catalogue:edit", "catalogue:approve"],
        "editor" => ["catalogue:edit"],
        _ => [],
    };

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(PersonHeader, out var subject) || !People.TryGetValue(subject.ToString(), out var person))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim>
            {
                new("sub", subject.ToString()), new("sid", Guid.NewGuid().ToString("D")),
                new("name", subject.ToString()), new("email", $"{subject}@example.test"),
            };
            if (person.Organization is { } organization && person.Role is { } role)
            {
                var id = organization.ToString("D", CultureInfo.InvariantCulture);
                claims.Add(new Claim(OrganizationClaimTypeConstants.Id, id));
                claims.Add(new Claim(OrganizationClaimTypeConstants.Role, $"{id}:{role}"));
                claims.AddRange(Permissions(role).Select(permission => new Claim(OrganizationClaimTypeConstants.Permission, $"{id}:{permission}")));
            }
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name, "name", "role"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
