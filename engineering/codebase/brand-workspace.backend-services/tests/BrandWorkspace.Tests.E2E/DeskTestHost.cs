using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BrandWorkspace.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;
using WoW.Two.Sdk.Beta.Be.Testing;

namespace BrandWorkspace.Tests.E2E;

/// <summary>What the Desk test hosts replace. Sign-in: a stand-in for the Identity BFF cookie scheme, where a request that
/// names a registered person in <see cref="PersonHeader"/> is signed in with the claims Identity's token carries and any other
/// request is anonymous; no call reaches the Identity instance. Release: a valid migration release, because a local build
/// carries the informational version <c>dev</c>, which the migrator refuses and CI replaces with the product version.</summary>
public static class DeskTestHost
{
    /// <summary>The API's application name, which a test host that migrates takes too: the module migration sets claim
    /// their schema as <c>{application}@local</c>, as Desk's own <c>--migrate</c> does.</summary>
    public static readonly string ApplicationName = typeof(ApiAssemblyMarker).Assembly.GetName().Name!;

    /// <summary>Names the registered person a request signs in as.</summary>
    public const string PersonHeader = "X-Test-Person";

    /// <summary>Settings that satisfy <c>Identity:Client</c> validation without any network call to the authority.</summary>
    public static IReadOnlyDictionary<string, string?> IdentityClientSettings { get; } = new Dictionary<string, string?>
    {
        ["Identity:Client:Authority"] = "https://identity.example.test/",
        ["Identity:Client:ClientId"] = "desk"
    };

    private static readonly ConcurrentDictionary<string, Person> People = new(StringComparer.Ordinal);

    /// <summary>A signed-in person; a null <paramref name="OrganizationId"/> or <paramref name="Role"/> carries no organization claims.</summary>
    public sealed record Person(string Subject, Guid? OrganizationId, string? Role, string Name, string Email);

    /// <summary>Registers, or replaces, the claims a person signs in with; later requests of any client see the change.</summary>
    public static void Register(Person person) => People[person.Subject] = person;

    /// <summary>Returns the registered person, or null.</summary>
    public static Person? Find(string subject) => People.GetValueOrDefault(subject);

    /// <summary>Replaces the BFF cookie scheme's handler with the header-driven test handler.</summary>
    public static void Use(IServiceCollection services)
    {
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            foreach (var scheme in options.Schemes.Where(scheme => scheme.Name == BffAuthenticationSchemeConstants.Cookie))
                scheme.HandlerType = typeof(Handler);
        });
    }

    /// <summary>A client over HTTPS that signs in as <paramref name="subject"/>; without one it stays anonymous.</summary>
    public static HttpClient CreateClient(WebApiTestHost<ApiAssemblyMarker> factory, string? subject = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });
        if (subject is not null) client.DefaultRequestHeaders.Add(PersonHeader, subject);
        return client;
    }

    /// <summary>The permissions Desk grants a role in the claims: owner edits and approves, editor edits, viewer reads.</summary>
    private static string[] Permissions(string role) => role switch
    {
        "owner" => ["desk:edit", "desk:approve"],
        "editor" => ["desk:edit"],
        _ => []
    };

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(PersonHeader, out var subject) || Find(subject.ToString()) is not { } person)
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim>
            {
                new("sub", person.Subject), new("sid", Guid.NewGuid().ToString("D")),
                new("name", person.Name), new("email", person.Email)
            };
            if (person.OrganizationId is { } organization && person.Role is { } role)
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
