using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Tests.Harness;

/// <summary>
/// An in-memory WoW2 Events digest store for one organization: what the Telegram delivery routes forward, with Events'
/// revision rule; a send reads every recipient but <see cref="Linked"/> as <c>not_linked</c>, as Identity answers for a
/// person with no Telegram link.
/// </summary>
internal sealed class FakeDigestsBroker : IEventDigestsBroker
{
    private static readonly DateTimeOffset At = new(2026, 11, 17, 4, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<Guid, DigestModel> digests = [];
    private readonly List<DigestRunModel> runs = [];

    /// <summary>Gets the subjects with a Telegram link; every other recipient reads <c>not_linked</c>.</summary>
    public HashSet<string> Linked { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the catalogs the routes asked for, in order.</summary>
    public List<string> Catalogs { get; } = [];

    public Task<Result<IReadOnlyList<DigestModel>>> ListAsync(string catalog, CancellationToken cancellationToken = default)
    {
        Catalogs.Add(catalog);
        return Task.FromResult(Result<IReadOnlyList<DigestModel>>.Ok(digests.Values.OrderBy(item => item.Name).ToList()));
    }

    public Task<Result<DigestModel>> GetAsync(string catalog, Guid id, CancellationToken cancellationToken = default)
    {
        Catalogs.Add(catalog);
        return Task.FromResult(digests.TryGetValue(id, out var digest) ? Result<DigestModel>.Ok(digest) : Missing<DigestModel>());
    }

    public Task<Result<DigestModel>> CreateAsync(string catalog, DigestSpec spec, CancellationToken cancellationToken = default)
    {
        Catalogs.Add(catalog);
        var digest = Model(Guid.NewGuid(), catalog, spec, 1);
        digests[digest.Id] = digest;
        return Task.FromResult(Result<DigestModel>.Ok(digest));
    }

    public Task<Result<DigestModel>> UpdateAsync(string catalog, Guid id, DigestSpec spec, CancellationToken cancellationToken = default)
    {
        Catalogs.Add(catalog);
        if (!digests.TryGetValue(id, out var current)) return Task.FromResult(Missing<DigestModel>());
        if (current.Revision != spec.ExpectedRevision)
            return Task.FromResult(Result<DigestModel>.Fail(AppError.Of(AppErrorType.Conflict, "Stale digest.", null, "", "", 0)));
        var digest = Model(id, catalog, spec, current.Revision + 1);
        digests[id] = digest;
        return Task.FromResult(Result<DigestModel>.Ok(digest));
    }

    public Task<Result<DigestModel>> DeleteAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default)
    {
        Catalogs.Add(catalog);
        if (!digests.Remove(id, out var removed)) return Task.FromResult(Missing<DigestModel>());
        return Task.FromResult(Result<DigestModel>.Ok(removed));
    }

    public Task<Result<DigestPreviewModel>> PreviewAsync(string catalog, Guid id, CancellationToken cancellationToken = default)
    {
        Catalogs.Add(catalog);
        if (!digests.TryGetValue(id, out var digest)) return Task.FromResult(Missing<DigestPreviewModel>());
        return Task.FromResult(Result<DigestPreviewModel>.Ok(new DigestPreviewModel
        {
            DigestId = id, AsOfUtc = At, Matched = 0, Items = [], Texts = [Text(digest)], Recipients = Recipients(digest, "pending"),
        }));
    }

    public Task<Result<DigestRunModel>> SendNowAsync(string catalog, Guid id, CancellationToken cancellationToken = default)
    {
        Catalogs.Add(catalog);
        if (!digests.TryGetValue(id, out var digest)) return Task.FromResult(Missing<DigestRunModel>());
        var run = new DigestRunModel
        {
            Id = Guid.NewGuid(), DigestId = id, OccurrenceAtUtc = At, Trigger = DigestRunTrigger.Manual,
            Status = DigestRunStatus.Partial, Matched = 0, Selected = 0, Texts = [Text(digest)],
            Recipients = Recipients(digest, null), StartedAtUtc = At, FinishedAtUtc = At,
        };
        runs.Insert(0, run);
        return Task.FromResult(Result<DigestRunModel>.Ok(run));
    }

    public Task<Result<DigestRunPage>> ListRunsAsync(string catalog, Guid id, DigestRunQuery query, CancellationToken cancellationToken = default)
    {
        Catalogs.Add(catalog);
        return Task.FromResult(Result<DigestRunPage>.Ok(new DigestRunPage(
            runs.Where(run => run.DigestId == id).Take(query.Limit).ToList(), null)));
    }

    private static DigestModel Model(Guid id, string catalog, DigestSpec spec, int revision) => new()
    {
        Id = id, Catalog = catalog, Name = spec.Name, Enabled = spec.Enabled, Channel = spec.Channel, Schedule = spec.Schedule,
        Selection = spec.Selection, Recipients = spec.Recipients, Language = spec.Language, ItemCap = spec.ItemCap,
        LinkTemplate = spec.LinkTemplate, NextRunAtUtc = spec.Enabled ? new DateTimeOffset(2026, 11, 23, 4, 0, 0, TimeSpan.Zero) : null,
        Revision = revision, CreatedAtUtc = At, UpdatedAtUtc = At,
    };

    private static DigestTextModel Text(DigestModel digest) =>
        new() { Language = digest.Language, Text = $"<b>{digest.Name}</b>", Recipients = digest.Recipients.Count };

    private List<DigestRunRecipientModel> Recipients(DigestModel digest, string? status) => digest.Recipients
        .Where(recipient => recipient.Subject is not null)
        .Select(recipient => new DigestRunRecipientModel
        {
            Subject = recipient.Subject!, Language = recipient.Language ?? digest.Language,
            Status = status ?? (Linked.Contains(recipient.Subject!) ? DigestRecipientStatusConstants.Sent : DigestRecipientStatusConstants.NotLinked),
            Attempts = status is null ? 1 : 0, UpdatedAtUtc = At,
        })
        .ToList();

    private static Result<T> Missing<T>() where T : notnull =>
        Result<T>.Fail(AppError.Of(AppErrorType.NotFound, "The digest was not found.", null, "", "", 0));
}
