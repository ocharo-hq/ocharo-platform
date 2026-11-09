using System.Globalization;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OcharoBrand.Tests.Support;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace OcharoBrand.Tests.Harness;

/// <summary>
/// What the Brand test hosts replace for sign-in: a stand-in for the Identity BFF cookie scheme. A request that names one of
/// the <see cref="People"/> in <see cref="PersonHeader"/> is signed in with the claims Identity's token carries and a session
/// reference, so the BFF's <c>X-Csrf-Token</c> check holds as for a real session; any other request is anonymous. No call
/// reaches the Identity instance.
/// </summary>
internal static class BrandTestIdentity
{
    /// <summary>Names the person a request signs in as.</summary>
    public const string PersonHeader = "X-Test-Person";

    /// <summary>The session cookie's name in the test hosts.</summary>
    public const string CookieName = "__Host-ocharo-brand";

    /// <summary>The organization the test hosts bind (<c>Brand:OrganizationId</c>).</summary>
    public static readonly Guid Organization = Guid.Parse("5f0c9b1e-4a7d-4c2e-9b8a-1d2e3f405162");

    /// <summary>A signed-in person; a null <paramref name="OrganizationId"/> carries no organization claims.</summary>
    public sealed record Person(string Subject, Guid? OrganizationId, string? Role, string Name, string Email);

    public static readonly Person Owner = new("owner-sub", Organization, "owner", "Malika Owner", "owner@example.test");
    public static readonly Person Editor = new("editor-sub", Organization, "editor", "Aziz Editor", "editor@example.test");
    public static readonly Person Viewer = new("viewer-sub", Organization, "viewer", "Nodira Viewer", "viewer@example.test");
    public static readonly Person Outsider = new("outsider-sub", Guid.Parse("0b6f1c2d-3e4f-4a5b-8c6d-7e8f90a1b2c3"), "owner", "Other Owner", "other@example.test");
    public static readonly Person Unaffiliated = new("unaffiliated-sub", null, null, "No Organization", "none@example.test");

    private static readonly Dictionary<string, Person> People = new[] { Owner, Editor, Viewer, Outsider, Unaffiliated }
        .ToDictionary(person => person.Subject, StringComparer.Ordinal);

    /// <summary>Settings that satisfy <c>Identity:Client</c> validation without any network call to the authority, bind the
    /// test organization, open <paramref name="database"/> (else a fresh product database, migrated as the deploy step
    /// leaves it) and point the retired SQLite store, its key ring and blobs under <paramref name="root"/>, never at the
    /// machine's own <c>App_Data</c>.</summary>
    public static Dictionary<string, string?> Settings(string root, string? database = null) => new()
    {
        ["Identity:Client:Authority"] = "https://identity.example.test/",
        ["Identity:Client:ClientId"] = "brand",
        ["Identity:Client:CookieName"] = CookieName,
        ["Brand:OrganizationId"] = Organization.ToString(),
        ["ConnectionStrings:Database"] = database ?? BrandTestDatabase.CreateAsync().GetAwaiter().GetResult(),
        [MigrationConstants.OwnerInstanceConfigurationKey] = BrandTestDatabase.OwnerInstance,
        ["LegacySqlite:Path"] = Path.Combine(root, "legacy", "ocharo-brand.db"),
        ["LegacySqlite:KeysPath"] = Path.Combine(root, "legacy", "keys"),
    };

    /// <summary>Replaces the BFF cookie scheme's handler with the header-driven test handler.</summary>
    public static void Use(IServiceCollection services)
    {
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            foreach (var scheme in options.Schemes.Where(scheme => scheme.Name == BffAuthenticationSchemeConstants.Cookie))
                scheme.HandlerType = typeof(Handler);
        });
    }

    /// <summary>A client over HTTPS that stays anonymous.</summary>
    public static HttpClient Anonymous(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), HandleCookies = false, AllowAutoRedirect = false });

    /// <summary>A client over HTTPS signed in as <paramref name="person"/>; with <paramref name="csrf"/> it sends the session's
    /// <c>X-Csrf-Token</c>, read from the session route as the SPA does, on every request.</summary>
    public static async Task<HttpClient> SignedIn(WebApplicationFactory<Program> factory, Person person, bool csrf = true)
    {
        var client = Anonymous(factory);
        client.DefaultRequestHeaders.Add(PersonHeader, person.Subject);
        if (csrf)
            client.DefaultRequestHeaders.Add("X-Csrf-Token", (await SessionAsync(client)).GetProperty("csrfToken").GetString());
        return client;
    }

    /// <summary>The session route's <c>data</c>.</summary>
    public static async Task<JsonElement> SessionAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/identity/session")).GetProperty("data");

    /// <summary>The permissions the deployed Identity grants a role: owner and admin edit and approve, editor edits, viewer reads.</summary>
    private static string[] Permissions(string role) => role switch
    {
        "owner" or "admin" => ["brand:edit", "brand:approve"],
        "editor" => ["brand:edit"],
        _ => []
    };

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(PersonHeader, out var subject) || !People.TryGetValue(subject.ToString(), out var person))
                return Task.FromResult(AuthenticateResult.NoResult());
            var session = "sid-" + person.Subject;
            var claims = new List<Claim> { new("sub", person.Subject), new("sid", session), new("name", person.Name), new("email", person.Email) };
            if (person.OrganizationId is { } organization && person.Role is { } role)
            {
                var id = organization.ToString("D", CultureInfo.InvariantCulture);
                claims.Add(new Claim(OrganizationClaimTypeConstants.Id, id));
                claims.Add(new Claim(OrganizationClaimTypeConstants.Role, $"{id}:{role}"));
                claims.Add(new Claim(OrganizationClaimTypeConstants.Name, "Ocharo"));
                claims.Add(new Claim(OrganizationClaimTypeConstants.Slug, "ocharo"));
                claims.AddRange(Permissions(role).Select(permission => new Claim(OrganizationClaimTypeConstants.Permission, $"{id}:{permission}")));
            }
            // The BFF's session reference (`.bff.sid`, `.bff.ref`): the session route then issues a CSRF token bound to it,
            // and the antiforgery check demands it on unsafe API requests.
            var properties = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) };
            properties.Items[".bff.sid"] = session;
            properties.Items[".bff.ref"] = "test-reference";
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name, "name", "role"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, properties, Scheme.Name)));
        }
    }
}
