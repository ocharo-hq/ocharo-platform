using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.FoundationServices;

namespace OcharoBrand.Tests.Unit;

/// <summary>What the next release would change, in the English labels the operator reads: every kind of difference the
/// draft can hold against the latest release, in the order the service lists them.</summary>
public sealed class BrandDraftDifferencesTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    private static BrandIdentity Identity() => new() { Name = "Ocharo" };

    private static LogoVersion Logo(string id, string slot, string fileName) =>
        new(id, slot, "sha", fileName, "image/png", 1, 1, 1, false, "approved", null, null, At);

    private static BrandRelease Release(BrandIdentity identity, params LogoVersion[] logos) =>
        new(1, At, "Release 1", identity, logos.ToDictionary(l => l.Slot));

    private static BrandDraft Draft(BrandIdentity identity, params LogoVersion[] logos) =>
        new() { Identity = identity, CurrentLogos = logos.ToDictionary(l => l.Slot, l => l.Id) };

    [Fact]
    public void Differences_ShouldBeEmpty_WhenDraftEqualsRelease()
    {
        var light = Logo("a", LogoSlots.OnLight, "light.png");

        var changes = BrandDraftService.Differences(Release(Identity(), light), Draft(Identity(), light), [light]);

        changes.Should().BeEmpty();
    }

    [Fact]
    public void Differences_ShouldNameEachChangedPartInEnglish_WhenDraftDiffersFromRelease()
    {
        var before = Identity();
        var after = before with
        {
            Name = "Ocharo Stil",
            Tagline = new LocalizedText(null, null, "New tagline"),
            Palette = [new BrandColor("ink", "Ink", "#111111", "text")],
            Typography = new BrandTypography("Georgia", "Inter", null),
            Links = [new BrandLink("site", "Website", null, "https://ocharo.example/")],
        };

        var changes = BrandDraftService.Differences(Release(before), Draft(after), []);

        changes.Should().Equal("Name: Ocharo → Ocharo Stil", "Texts", "Colors", "Fonts", "Links");
    }

    [Fact]
    public void Differences_ShouldNameTheNewFileOrSayRemoved_WhenALogoSlotChanges()
    {
        var oldLight = Logo("old", LogoSlots.OnLight, "light-v1.png");
        var newLight = Logo("new", LogoSlots.OnLight, "light-v2.png");
        var dark = Logo("dark", LogoSlots.OnDark, "dark.png");

        var changes = BrandDraftService.Differences(Release(Identity(), oldLight, dark), Draft(Identity(), newLight), [oldLight, newLight, dark]);

        changes.Should().Equal("Logo on-light: light-v2.png", "Logo on-dark: removed");
    }

    [Fact]
    public void Differences_ShouldReadEnglishOnly_WhenEveryPartChanges()
    {
        var before = Identity();
        var after = before with
        {
            Name = "Other",
            SpellingRule = "Other, never OTHER",
            Palette = [new BrandColor("ink", "Ink", "#111111", "text")],
            Typography = new BrandTypography("Georgia", "Inter", null),
            Links = [new BrandLink("site", "Website", null, "https://ocharo.example/")],
        };
        var removed = Logo("old", LogoSlots.Master, "master.png");

        var changes = BrandDraftService.Differences(Release(before, removed), Draft(after), [removed]);

        string.Join(' ', changes).Should().NotContainAny("Nom", "Matnlar", "Ranglar", "Shriftlar", "Havolalar", "Logotip", "olib tashlangan");
    }
}
