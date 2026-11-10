using BrandWorkspace.Api.RuntimeConfig.Models;

namespace BrandWorkspace.Api.RuntimeConfig.Settings;

/// <summary>Projects the Ocharo app addresses onto the runtime-config keys the Desk SPA declares.</summary>
public static class OcharoAppsSettingsExtensions
{
    /// <summary>Pairs each setting with its runtime-config key, in the app menu's order; <see cref="RuntimeConfigDto"/>
    /// names the same keys.</summary>
    private static readonly (string Setting, string Key, Func<OcharoAppsSettings, string?> Read)[] Addresses =
    [
        (nameof(OcharoAppsSettings.Desk), "OCHARO_DESK_URL", settings => settings.Desk),
        (nameof(OcharoAppsSettings.Catalogue), "OCHARO_CATALOGUE_URL", settings => settings.Catalogue),
        (nameof(OcharoAppsSettings.Studio), "OCHARO_STUDIO_URL", settings => settings.Studio),
        (nameof(OcharoAppsSettings.Marketing), "OCHARO_MARKETING_URL", settings => settings.Marketing),
        (nameof(OcharoAppsSettings.Editorial), "OCHARO_EDITORIAL_URL", settings => settings.Editorial),
        (nameof(OcharoAppsSettings.Brand), "OCHARO_BRAND_URL", settings => settings.Brand),
        (nameof(OcharoAppsSettings.Atlas), "OCHARO_ATLAS_URL", settings => settings.Atlas),
        (nameof(OcharoAppsSettings.Wear), "OCHARO_WEAR_URL", settings => settings.Wear),
        (nameof(OcharoAppsSettings.Blog), "OCHARO_BLOG_URL", settings => settings.Blog),
        (nameof(OcharoAppsSettings.Events), "OCHARO_EVENTS_URL", settings => settings.Events),
    ];

    /// <summary>
    /// Returns the configured, plain HTTPS addresses under their runtime-config names; any other value is left out.
    /// </summary>
    public static RuntimeConfigDto ToRuntimeConfig(this OcharoAppsSettings settings)
    {
        string? Public(Func<OcharoAppsSettings, string?> read)
        {
            return read(settings) is { } value && IsPublicAddress(value) ? value : null;
        }
        return new RuntimeConfigDto
        {
            Desk = Public(apps => apps.Desk),
            Catalogue = Public(apps => apps.Catalogue),
            Studio = Public(apps => apps.Studio),
            Marketing = Public(apps => apps.Marketing),
            Editorial = Public(apps => apps.Editorial),
            Brand = Public(apps => apps.Brand),
            Atlas = Public(apps => apps.Atlas),
            Wear = Public(apps => apps.Wear),
            Blog = Public(apps => apps.Blog),
            Events = Public(apps => apps.Events),
        };
    }

    /// <summary>
    /// Names the settings that are configured but not plain HTTPS addresses, so the host can warn about them.
    /// </summary>
    public static IReadOnlyList<string> RejectedSettings(this OcharoAppsSettings settings)
    {
        return Addresses
            .Where(address =>
                !string.IsNullOrWhiteSpace(address.Read(settings)) && !IsPublicAddress(address.Read(settings)))
            .Select(address => $"{OcharoAppsSettings.SectionName}:{address.Setting}")
            .ToArray();
    }

    /// <summary>
    /// Accepts an absolute HTTPS address without credentials, query or fragment that could carry a token.
    /// </summary>
    public static bool IsPublicAddress(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Length > 0
            && uri.UserInfo.Length == 0
            && value.IndexOfAny(['?', '#']) < 0
            && !value.Any(char.IsWhiteSpace);
    }
}
