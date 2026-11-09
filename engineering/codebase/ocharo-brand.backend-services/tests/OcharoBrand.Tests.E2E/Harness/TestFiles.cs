using System.Collections.Concurrent;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoBrand.Tests.Harness;

/// <summary>An in-memory Files tenant: uploads keep their bytes, idempotency key and app; references attach; links
/// name the file and the asked width.</summary>
internal sealed class TestFiles : IFileUploadsBroker, IFileStoreBroker, IFileReferencesBroker, IFileUrlsBroker
{
    /// <summary>The Files address the Brand hosts are given (<c>Files:BaseUrl</c>).</summary>
    public const string BaseUrl = "https://files.example.test/";

    private readonly ConcurrentDictionary<FileId, FileModel> files = new();
    private readonly ConcurrentDictionary<string, FileId> keys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<FileId, ConcurrentBag<FileReference>> references = new();

    public int Count => files.Count;

    public static Uri Link(FileId id, int? width) => new($"{BaseUrl}f/{id}?w={width}");

    public FileModel BySha(string sha) => files.Values.Single(file => file.Sha256 == sha);

    public IReadOnlyCollection<FileReference> ReferencesOf(FileId id) => references.TryGetValue(id, out var bag) ? bag : [];

    /// <summary>Holds a file as the migration would leave it: moved, with its reference attached.</summary>
    public FileModel Add(byte[] bytes, string appId, FileReference reference)
    {
        var file = Model(bytes, $"{appId}-moved", appId);
        files[file.Id] = file;
        references.GetOrAdd(file.Id, _ => []).Add(reference);
        return file;
    }

    /// <summary>The failure every later upload answers; none uploads.</summary>
    public AppError? UploadRefusal { get; set; }

    public async Task<Result<FileModel>> UploadAsync(Stream content, UploadSpec spec, CancellationToken cancellationToken = default)
    {
        if (UploadRefusal is { } refusal)
            return Result<FileModel>.Fail(refusal);
        if (spec.IdempotencyKey is { } key && keys.TryGetValue(key, out var first))
            return Result<FileModel>.Ok(files[first]);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var file = Model(buffer.ToArray(), spec.Name, spec.AppId);
        files[file.Id] = file;
        if (spec.IdempotencyKey is { } idempotency)
            keys[idempotency] = file.Id;
        return Result<FileModel>.Ok(file);
    }

    public Task<Result> AttachAsync(FileId id, FileReference reference, CancellationToken cancellationToken = default)
    {
        references.GetOrAdd(id, _ => []).Add(reference);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result<SignedFileUrl>> IssueAsync(FileId id, UrlSpec spec, CancellationToken cancellationToken = default) =>
        Task.FromResult(files.ContainsKey(id)
            ? Result<SignedFileUrl>.Ok(new SignedFileUrl(Link(id, spec.Width), DateTimeOffset.UtcNow + spec.Lifetime))
            : Result<SignedFileUrl>.Fail(WoW.Two.Sdk.Beta.Be.Foundation.Errors.AppErrorFactory.NotFound("No such file.")));

    public Task<Result<FilePage>> ListAsync(FileQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<FilePage>.Ok(new FilePage(files.Values.Where(file => query.AppId is null || file.AppId == query.AppId).ToList(), null)));

    public Task<Result<FileModel>> GetAsync(FileId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(files.TryGetValue(id, out var file)
            ? Result<FileModel>.Ok(file)
            : Result<FileModel>.Fail(AppErrorFactory.NotFound("No such file.")));

    public Task<Result<FileContent>> OpenReadAsync(FileId id, ByteRange? range = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Result<FileModel>> UpdateAsync(FileId id, FilePatch patch, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public bool RestoreFails { get; set; }

    public FileModel ById(FileId id) => files[id];

    public Task<Result<FileModel>> DeleteAsync(FileId id, CancellationToken cancellationToken = default)
    {
        files[id] = files[id] with { DeletedAtUtc = DateTimeOffset.UtcNow };
        return Task.FromResult(Result<FileModel>.Ok(files[id]));
    }

    public Task<Result<FileModel>> RestoreAsync(FileId id, CancellationToken cancellationToken = default)
    {
        if (RestoreFails) return Task.FromResult(Result<FileModel>.Fail(AppErrorFactory.NotFound("Past restoring.")));
        files[id] = files[id] with { DeletedAtUtc = null };
        return Task.FromResult(Result<FileModel>.Ok(files[id]));
    }

    public Task<Result> DetachAsync(FileId id, FileReference reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Result<IReadOnlyList<FileReference>>> ListAsync(FileId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Result<Uri>> GetPublicUrlAsync(FileId id, int? width = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    private static FileModel Model(byte[] bytes, string name, string? appId) => new()
    {
        Id = FileId.New(),
        TenantId = "ocharo",
        AppId = appId,
        Name = name,
        ContentType = "image/png",
        Kind = FileKind.Image,
        Size = bytes.LongLength,
        Sha256 = TestArtwork.Sha(bytes),
        Visibility = FileVisibility.Private,
        State = FileState.Ready,
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };
}
