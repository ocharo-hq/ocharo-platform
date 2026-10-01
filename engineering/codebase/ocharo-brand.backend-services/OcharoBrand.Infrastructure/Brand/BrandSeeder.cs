using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OcharoBrand.Application.Brand;
using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Infrastructure.Brand;

/// <summary>
/// Seeds an empty brand from Maftuna's recorded brand decisions (spelling rule, card copy, handles, palette) and the
/// approved logo files, then publishes release 1 so every app has a kit to read. Unknown facts (phone, taglines)
/// stay empty rather than invented.
/// </summary>
public sealed class BrandSeeder(IBrandRepository repository, BrandService brand, IOptions<BrandOptions> options, ILogger<BrandSeeder> logger)
{
    private static readonly (string Slot, string File, string Status, string Note)[] Logos =
    [
        (LogoSlots.Master, "och-logo-approved-ivory-v1.png", LogoStatus.Approved, "Approved tilted-needle master, preserved from the selected output (2026-09-10)."),
        (LogoSlots.OnLight, "och-logo-black-transparent-v1.png", LogoStatus.Draft, "Black lettering for light surfaces; raster export draft with slight contour differences from the master."),
        (LogoSlots.OnDark, "och-logo-white-transparent-v1.png", LogoStatus.Draft, "White lettering for dark surfaces; raster export draft with slight contour differences from the master."),
    ];

    public static BrandIdentity DefaultIdentity() => new()
    {
        Name = "Ocharo",
        SpellingRule = "Write Ocharo (capital O, lowercase charo) in every language and script; never OCHARO or a Cyrillic transliteration. Account handles keep their exact spelling.",
        Descriptor = new LocalizedText("Zamonaviy milliy liboslar", "Современная узбекская мода", "Contemporary Uzbek fashion"),
        Tagline = new LocalizedText(null, null, null),
        Location = new LocalizedText("Toshkent", "Ташкент, Узбекистан", "Tashkent, Uzbekistan"),
        Palette =
        [
            new BrandColor("ink", "Charcoal ink", "#20211e", "text"),
            new BrandColor("paper", "Warm ivory", "#f8f7f3", "background"),
            new BrandColor("accent", "Vermilion", "#dd492f", "accent"),
            new BrandColor("muted", "Stone", "#646760", "muted"),
            new BrandColor("line", "Hairline", "#deded6", "border"),
        ],
        Typography = new BrandTypography("Georgia", "Arial", "Cards: Georgia 14 pt for the name, Arial for supporting text."),
        Links =
        [
            new BrandLink("instagram", "Instagram", "ocharostile", "https://www.instagram.com/ocharostile/"),
            new BrandLink("telegram", "Telegram", "Ocharo_Style", "https://t.me/Ocharo_Style"),
        ],
    };

    public async Task SeedIfEmptyAsync(CancellationToken ct)
    {
        if (await repository.FindDraftAsync(ct) is not null) return;
        var current = new Dictionary<string, string>();
        if (options.Value.SeedLogoRoot is { } root && Directory.Exists(root))
        {
            foreach (var (slot, file, status, note) in Logos)
            {
                var path = Path.Combine(root, file);
                if (!File.Exists(path)) { logger.LogWarning("Seed logo {File} not found", file); continue; }
                var version = await brand.UploadLogoAsync(slot, await File.ReadAllBytesAsync(path, ct), file, note, ct, path, status);
                current[slot] = version.Id;
            }
        }

        var draft = new BrandDraft { Identity = DefaultIdentity(), CurrentLogos = current };
        await repository.SaveDraftAsync(brand.Record(draft, 1), 0, "Seeded from the Ocharo brand guidelines and approved logo files", ct);
        if (current.Count > 0)
            await brand.PublishAsync(new PublishInput(1, "Imported from Maftuna's brand guidelines and logo files (exports are raster drafts)."), ct);
        logger.LogInformation("Seeded brand with {Logos} logo files", current.Count);
    }
}
