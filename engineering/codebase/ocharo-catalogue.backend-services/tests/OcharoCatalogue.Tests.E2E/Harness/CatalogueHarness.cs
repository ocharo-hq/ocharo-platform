using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Time.Testing;
using OcharoCatalogue.Api.Configurations;
using SkiaSharp;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>A fresh PostgreSQL database + real blob folder per test, wired exactly like the host; optionally on an in-memory
/// WoW2 Files.</summary>
public sealed class CatalogueHarness : IAsyncDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-tests", Guid.NewGuid().ToString("N"));
    public ServiceProvider Services { get; }
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero));

    private CatalogueHarness(string? sourceRoot, TestFiles? files, IReadOnlyDictionary<string, string?>? overrides)
    {
        Directory.CreateDirectory(Root);
        var settings = new Dictionary<string, string?>
        {
            [CatalogueTestDatabase.ConnectionKey] = CatalogueTestDatabase.For(Path.Combine(Root, "catalogue.db")), [CatalogueTestDatabase.OwnerKey] = CatalogueTestDatabase.OwnerInstance,
            ["Catalogue:DataRoot"] = Path.Combine(Root, "data"),
            ["Catalogue:SourceRoot"] = sourceRoot,
            ["Files:BaseUrl"] = files is null ? null : TestFiles.Url,
        };
        foreach (var (key, value) in overrides ?? new Dictionary<string, string?>())
            settings[key] = value;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(HostConfiguration).Assembly.GetName().Name,
            ContentRootPath = Root,
            EnvironmentName = "Testing",
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Configure();
        builder.Services.AddSingleton<TimeProvider>(Clock);
        files?.Register(builder.Services);
        Services = builder.Services.BuildServiceProvider();
    }

    /// <summary>Creates a harness; with <paramref name="files"/>, Catalogue stores in that in-memory WoW2 Files.</summary>
    /// <remarks>The database comes from the template, which holds every set at this build's level, so the store starts
    /// as a migrated, empty catalogue.</remarks>
    public static Task<CatalogueHarness> CreateAsync(
        string? sourceRoot = null, TestFiles? files = null, IReadOnlyDictionary<string, string?>? settings = null) =>
        Task.Run(() => new CatalogueHarness(sourceRoot, files, settings));

    public T Get<T>() where T : notnull => Services.CreateScope().ServiceProvider.GetRequiredService<T>();

    public CatalogueClient Client => new(Services);
    public TestProducts Products => new(Client);
    public TestCollections Collections => new(Client);
    public TestSettings Settings => new(Client);
    public TestFeed Feed => new(Client);
    public TestDesigns Designs => new(Client);
    public TestReleases Releases => new(Client);
    public TestExports Exports => new(Client);
    public TestImporter Importer => new(Client);

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        try { Directory.Delete(Root, true); } catch (IOException) { }
    }

    /// <summary>A small opaque/transparent PNG with a distinct colour so hashes differ.</summary>
    public static byte[] Png(byte shade, int width = 60, int height = 90)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(new SKColor(shade, (byte)(255 - shade), 90, 255));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Writes a workspace-shaped product folder (schema 1.3 subset) with one cutout and one model image.</summary>
    public static string WriteWorkspaceProduct(string root, string id, string garment, long retail, long wholesale, byte shade)
    {
        var folder = Path.Combine(root, id);
        Directory.CreateDirectory(Path.Combine(folder, "references"));
        Directory.CreateDirectory(Path.Combine(folder, "models"));
        File.WriteAllBytes(Path.Combine(folder, "references", $"och-{id}-front-transparent.png"), Png(shade));
        File.WriteAllBytes(Path.Combine(folder, "models", $"och-{id}-model.png"), Png((byte)(shade + 9)));
        var json = new JsonObject
        {
            ["schema_version"] = "1.3",
            ["id"] = id,
            ["brand"] = "OCHARO",
            ["source"] = new JsonObject { ["language"] = "uz", ["asset"] = "user-message", ["kind"] = "text", ["verbatim"] = "Material: sintetika" },
            ["locales"] = new JsonObject
            {
                ["uz"] = new JsonObject { ["status"] = "draft", ["name"] = "Nimcha", ["fabric"] = "Sintetik mato", ["decoration"] = "Yo‘q", ["description"] = null },
                ["ru"] = new JsonObject { ["status"] = "draft", ["name"] = "Жилет", ["fabric"] = null, ["decoration"] = null, ["description"] = null },
                ["en"] = new JsonObject { ["status"] = "draft", ["name"] = "Vest", ["fabric"] = null, ["decoration"] = null, ["description"] = null },
            },
            ["sizes"] = new JsonArray("S", "M", "L"),
            ["classification"] = new JsonObject
            {
                ["status"] = "confirmed", ["design_id"] = null, ["variant_id"] = id[^3..], ["garment_type"] = garment,
                ["fabric_codes"] = new JsonArray(), ["decoration_codes"] = new JsonArray(),
            },
            ["assets"] = new JsonObject
            {
                ["source_images"] = new JsonArray(), ["baseline_id"] = null,
                ["model_images"] = new JsonArray($"models/och-{id}-model.png"),
                ["cutout_images"] = new JsonArray($"references/och-{id}-front-transparent.png", "references/missing.png"),
            },
            ["review"] = new JsonObject
            {
                ["status"] = "TBD",
                ["issues"] = new JsonArray(
                    new JsonObject { ["field"] = "pricing", ["status"] = "TBD", ["question"] = "Confirm 2–9 pieces." },
                    new JsonObject { ["field"] = "sizes", ["status"] = "resolved", ["question"] = "Sizes confirmed." }),
            },
            ["pricing"] = new JsonObject
            {
                ["currency"] = "UZS", ["basis"] = "per_piece", ["status"] = "user_confirmed",
                ["tiers"] = new JsonArray(
                    new JsonObject { ["minimum_quantity"] = 1, ["maximum_quantity"] = 1, ["unit_price"] = retail },
                    new JsonObject { ["minimum_quantity"] = 10, ["maximum_quantity"] = null, ["unit_price"] = wholesale }),
                ["quantity_rule_source"] = "test", ["unconfirmed_quantity_ranges"] = new JsonArray(), ["wording_proposal"] = "Narx miqdorga qarab.",
            },
        };
        File.WriteAllText(Path.Combine(folder, $"och-{id}.json"), json.ToJsonString());
        return folder;
    }
}
