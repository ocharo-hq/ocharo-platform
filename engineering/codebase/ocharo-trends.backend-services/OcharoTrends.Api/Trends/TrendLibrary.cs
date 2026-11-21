using System.Text.Json;

namespace OcharoTrends.Api.Trends;

internal sealed record Video(
    string Id,
    string Title,
    string Url,
    string Creator,
    string Format,
    string? PublishedAt = null,
    string Status = Review.Unreviewed,
    string Note = "",
    string[]? Tags = null)
{
    internal Video Apply(Review review) => this with { Status = review.Status, Note = review.Note };
}

internal sealed record Guide(string Slug, string Title, string Kind, string Summary, string[] Steps, GuideExample[] Examples);
internal sealed record GuideExample(string Title, string Url, int? StartSeconds);
internal sealed record Intake(Video[] Videos);
internal sealed record Review(string Status, string Note)
{
    internal const string Unreviewed = "unreviewed";
    internal const string Useful = "useful";
    internal const string Skipped = "skipped";

    internal bool IsValid() => Status is Unreviewed or Useful or Skipped
        && Note is { Length: <= 4000 } && !Note.Contains('\0');
}

internal sealed class TrendLibrary
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, Video> videosById;

    internal TrendLibrary(IConfiguration configuration, IHostEnvironment environment)
    {
        var seedPaths = configuration.GetSection("Trends:AdditionalSeedPaths").GetChildren()
            .Prepend(configuration.GetSection("Trends:SeedPath"));
        var videos = new List<Video>();
        videosById = new Dictionary<string, Video>(StringComparer.Ordinal);
        foreach (var seedPath in seedPaths)
        {
            var intake = Read<Intake>(seedPath, environment);
            if (intake is null) continue;

            var sourceVideos = intake.Videos
                ?? throw new InvalidDataException($"{seedPath.Path} must contain a videos array.");
            foreach (var video in sourceVideos)
            {
                if (video is null || !HasText(video.Id) || !HasText(video.Title) || !HasText(video.Creator)
                    || video.Format is not ("short" or "video") || !IsHttpsUrl(video.Url)
                    || !new Review(video.Status, video.Note).IsValid())
                    throw new InvalidDataException($"Each video in {seedPath.Path} needs an ID, title, creator, format and HTTPS source URL.");
                if (!videosById.TryAdd(video.Id, video))
                    throw new InvalidDataException($"Duplicate intake video ID '{video.Id}' in {seedPath.Path}.");

                videos.Add(video);
            }
        }
        Videos = videos;

        // Published guides are loaded from a separate snapshot with an array at the root.
        PublishedGuides = Read<Guide[]>(configuration.GetSection("Trends:PublishedGuidePath"), environment) ?? [];
        var slugs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var guide in PublishedGuides)
        {
            if (!HasText(guide.Slug) || !slugs.Add(guide.Slug) || !HasText(guide.Title) || !HasText(guide.Summary)
                || guide.Kind is not ("trend" or "transition") || guide.Steps is not { Length: > 0 }
                || guide.Steps.Any(step => !HasText(step)) || guide.Examples is null
                || guide.Examples.Any(example => !HasText(example.Title) || !IsHttpsUrl(example.Url) || example.StartSeconds < 0))
                throw new InvalidDataException("Each published guide needs a unique slug, title, kind, summary, steps and valid examples.");
        }
    }

    internal IReadOnlyList<Video> Videos { get; }
    internal IReadOnlyList<Guide> PublishedGuides { get; }
    internal Video? FindVideo(string id) => videosById.GetValueOrDefault(id);

    private static T? Read<T>(IConfigurationSection source, IHostEnvironment environment) where T : class
    {
        var path = source.Value;
        if (string.IsNullOrWhiteSpace(path)) return null;

        using var stream = File.OpenRead(Path.GetFullPath(path, environment.ContentRootPath));
        return JsonSerializer.Deserialize<T>(stream, Json)
            ?? throw new InvalidDataException($"{source.Path} must contain JSON data.");
    }

    private static bool HasText(string? text) => !string.IsNullOrWhiteSpace(text);

    private static bool IsHttpsUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0;
}
