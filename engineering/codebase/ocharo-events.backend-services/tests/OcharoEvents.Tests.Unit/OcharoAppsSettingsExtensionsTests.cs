using OcharoEvents.Api.RuntimeConfig.Settings;

namespace OcharoEvents.Tests.Unit;

/// <summary>Which configured Ocharo app addresses the runtime config may publish: only plain absolute HTTPS links.</summary>
public sealed class OcharoAppsSettingsExtensionsTests
{
    [Theory]
    [InlineData("https://ocharo-desk-dev.example.ts.net/", true)]
    [InlineData("https://desk.example.com:8443/organizations", true)]
    [InlineData("https://example.com/@handle", true)]
    [InlineData("http://desk.example.com/", false)]
    [InlineData("https://user@desk.example.com/", false)]
    [InlineData("https://desk.example.com/?", false)]
    [InlineData("https://desk.example.com/#", false)]
    [InlineData("https://desk.example.com/a b", false)]
    [InlineData(" https://desk.example.com/", false)]
    [InlineData("//desk.example.com/", false)]
    [InlineData("/organizations", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPublicAddress_ShouldAcceptOnlyPlainAbsoluteHttpsLinks(string? value, bool accepted)
    {
        OcharoAppsSettingsExtensions.IsPublicAddress(value).Should().Be(accepted);
    }

    [Fact]
    public void RejectedSettings_ShouldNameConfiguredAddressesItLeavesOut()
    {
        var settings = new OcharoAppsSettings
        {
            Desk = "http://desk.example.com/", Atlas = "https://atlas.example.com/", Wear = " ", Events = "https://events.example.com/?token=x",
        };

        settings.RejectedSettings().Should().BeEquivalentTo(["OcharoApps:Desk", "OcharoApps:Events"], options => options.WithStrictOrdering());
    }
}
