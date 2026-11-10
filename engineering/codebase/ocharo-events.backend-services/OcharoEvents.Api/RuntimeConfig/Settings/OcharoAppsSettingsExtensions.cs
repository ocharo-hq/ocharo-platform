using OcharoEvents.Api.RuntimeConfig.Models;

namespace OcharoEvents.Api.RuntimeConfig.Settings;

/// <summary>Projects the Ocharo app addresses onto the runtime-config keys the Events SPA declares.</summary>
public static class OcharoAppsSettingsExtensions
{
    /// <summary>Names each address setting, in the app menu's order.</summary>
    private static readonly (string Setting, Func<OcharoAppsSettings, string?> Read)[] Addresses =
    [
        (nameof(OcharoAppsSettings.Desk), settings => settings.Desk),
        (nameof(OcharoAppsSettings.Catalogue), settings => settings.Catalogue),
        (nameof(OcharoAppsSettings.Studio), settings => settings.Studio),
        (nameof(OcharoAppsSettings.Marketing), settings => settings.Marketing),
        (nameof(OcharoAppsSettings.Editorial), settings => settings.Editorial),
        (nameof(OcharoAppsSettings.Brand), settings => settings.Brand),
        (nameof(OcharoAppsSettings.Atlas), settings => settings.Atlas),
        (nameof(OcharoAppsSettings.Wear), settings => settings.Wear),
        (nameof(OcharoAppsSettings.Blog), settings => settings.Blog),
        (nameof(OcharoAppsSettings.Events), settings => settings.Events),
    ];

    /// <summary>Returns the configured, plain HTTPS addresses as runtime config; any other value is left out.</summary>
    public static RuntimeConfigDto ToRuntimeConfig(this OcharoAppsSettings settings)
    {
        return new RuntimeConfigDto
        {
            DeskUrl = Served(settings.Desk),
            CatalogueUrl = Served(settings.Catalogue),
            StudioUrl = Served(settings.Studio),
            MarketingUrl = Served(settings.Marketing),
            EditorialUrl = Served(settings.Editorial),
            BrandUrl = Served(settings.Brand),
            AtlasUrl = Served(settings.Atlas),
            WearUrl = Served(settings.Wear),
            BlogUrl = Served(settings.Blog),
            EventsUrl = Served(settings.Events),
        };

        static string? Served(string? value) => IsPublicAddress(value) ? value : null;
    }

    /// <summary>Names the settings that are configured but not plain HTTPS addresses, so the host can warn about them.</summary>
    public static IReadOnlyList<string> RejectedSettings(this OcharoAppsSettings settings)
    {
        return Addresses
            .Where(address => !string.IsNullOrWhiteSpace(address.Read(settings)) && !IsPublicAddress(address.Read(settings)))
            .Select(address => $"{OcharoAppsSettings.SectionName}:{address.Setting}")
            .ToArray();
    }

    /// <summary>Accepts an absolute HTTPS address without credentials, query or fragment that could carry a token.</summary>
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
