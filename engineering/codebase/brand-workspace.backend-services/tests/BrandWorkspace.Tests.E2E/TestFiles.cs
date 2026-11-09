using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;

namespace BrandWorkspace.Tests.E2E;

/// <summary>
/// An in-memory Files tenant, copied from Catalogue's and Marketing's tests: uploads keep their bytes, idempotency key,
/// app and visibility; references attach and list; links name the file, the asked width and disposition; reads serve
/// the kept bytes. An <see cref="Outage"/> fails every call, as an unreachable Files does.
/// </summary>
public sealed class TestFiles : IFileUploadsBroker, IFileStoreBroker, IFileReferencesBroker, IFileUrlsBroker
{
    /// <summary>Holds the Files root the tests configure.</summary>
    public const string Url = "https://files.example.test/";

    private readonly ConcurrentDictionary<FileId, FileModel> files = new();
    private readonly ConcurrentDictionary<FileId, byte[]> contents = new();
    private readonly ConcurrentDictionary<string, FileId> keys = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<FileId, ConcurrentBag<FileReference>> references = new();
    private int uploads;

    public int Uploads => uploads;

    /// <summary>Gets or sets the error every call answers with; none for a reachable Files.</summary>
    public AppError? Outage { get; set; }

    /// <summary>Puts this tenant behind every Files broker a Desk host resolves.</summary>
    public void Register(IServiceCollection services)
    {
        services.AddSingleton(this);
        services.AddSingleton<IFileUploadsBroker>(this);
        services.AddSingleton<IFileStoreBroker>(this);
        services.AddSingleton<IFileReferencesBroker>(this);
        services.AddSingleton<IFileUrlsBroker>(this);
    }

    /// <summary>Lists the files an app holds.</summary>
    public IReadOnlyList<FileModel> Of(string appId) => files.Values.Where(file => file.AppId == appId).ToList();

    public static Uri Link(FileId id, int? width, FileDisposition disposition = FileDisposition.Inline) =>
        new($"{Url}f/{id}?w={width}{(disposition == FileDisposition.Attachment ? "&d=attachment" : "")}");

    public static Uri Link(string id, int? width, FileDisposition disposition = FileDisposition.Inline) =>
        Link(FileId.TryParse(id, out var parsed) ? parsed : default, width, disposition);

    public FileModel Model(string id) => files[FileId.TryParse(id, out var parsed) ? parsed : default];

    public byte[] Content(string id) => contents[FileId.TryParse(id, out var parsed) ? parsed : default];

    /// <summary>The idempotency key a file was uploaded under; none for a file the migration moved.</summary>
    public string? KeyOf(string id) => keys.FirstOrDefault(pair => pair.Value.ToString() == id).Key;

    public IReadOnlyCollection<FileReference> ReferencesOf(string id) =>
        references.TryGetValue(FileId.TryParse(id, out var parsed) ? parsed : default, out var bag) ? bag : [];

    /// <summary>Holds a file as the migration would leave it: moved, with its reference attached.</summary>
    public FileModel Add(byte[] bytes, string appId, FileReference reference, string contentType = "image/png")
    {
        var file = NewModel(bytes, $"{appId}-moved", appId, contentType, FileVisibility.Private);
        files[file.Id] = file;
        contents[file.Id] = bytes;
        references.GetOrAdd(file.Id, _ => []).Add(reference);
        return file;
    }

    public async Task<Result<FileModel>> UploadAsync(
        Stream content,
        UploadSpec spec,
        CancellationToken cancellationToken = default)
    {
        if (Outage is { } outage)
            return Result<FileModel>.Fail(outage);
        if (spec.IdempotencyKey is { } key && keys.TryGetValue(key, out var first))
            return Result<FileModel>.Ok(files[first]);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var file = NewModel(
            buffer.ToArray(),
            spec.Name,
            spec.AppId,
            spec.ContentType ?? "application/octet-stream",
            spec.Visibility);
        files[file.Id] = file;
        contents[file.Id] = buffer.ToArray();
        Interlocked.Increment(ref uploads);
        if (spec.IdempotencyKey is { } idempotency)
            keys[idempotency] = file.Id;
        return Result<FileModel>.Ok(file);
    }

    public Task<Result> AttachAsync(FileId id, FileReference reference, CancellationToken cancellationToken = default)
    {
        if (Outage is { } outage)
            return Task.FromResult(Result.Fail(outage));
        if (!files.ContainsKey(id))
            return Task.FromResult(Result.Fail(AppErrorFactory.NotFound("No such file.")));
        var bag = references.GetOrAdd(id, _ => []);
        if (!bag.Contains(reference)) bag.Add(reference);
        return Task.FromResult(Result.Ok());
    }

    public Task<Result<IReadOnlyList<FileReference>>> ListAsync(
        FileId id,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Outage is { } outage ? Result<IReadOnlyList<FileReference>>.Fail(outage)
            : Result<IReadOnlyList<FileReference>>.Ok(references.TryGetValue(id, out var bag) ? bag.ToList() : []));
    }

    public Task<Result<SignedFileUrl>> IssueAsync(
        FileId id,
        UrlSpec spec,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Outage is { } outage ? Result<SignedFileUrl>.Fail(outage)
            : files.ContainsKey(id)
                ? Result<SignedFileUrl>.Ok(new SignedFileUrl(
                    Link(id, spec.Width, spec.Disposition),
                    DateTimeOffset.UtcNow + spec.Lifetime))
                : Result<SignedFileUrl>.Fail(AppErrorFactory.NotFound("No such file.")));
    }

    public Task<Result<FileModel>> GetAsync(FileId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(files.TryGetValue(id, out var file)
            ? Result<FileModel>.Ok(file)
            : Result<FileModel>.Fail(AppErrorFactory.NotFound("No such file.")));

    public Task<Result<FilePage>> ListAsync(FileQuery query, CancellationToken cancellationToken = default) =>
        Task.FromResult(Outage is { } outage ? Result<FilePage>.Fail(outage)
            : Result<FilePage>.Ok(new FilePage(
                files.Values.Where(file => query.AppId is null || file.AppId == query.AppId).ToList(),
                null)));

    public Task<Result<FileContent>> OpenReadAsync(
        FileId id,
        ByteRange? range = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Outage is { } outage ? Result<FileContent>.Fail(outage)
            : files.TryGetValue(id, out var file) && contents.TryGetValue(id, out var bytes)
                ? Result<FileContent>.Ok(new FileContent(
                    new MemoryStream(bytes, false),
                    file.ContentType,
                    bytes.LongLength,
                    bytes.LongLength,
                    null))
                : Result<FileContent>.Fail(AppErrorFactory.NotFound("No such file.")));
    }

    public Task<Result<FileModel>> UpdateAsync(
        FileId id,
        FilePatch patch,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<Result<FileModel>> DeleteAsync(FileId id, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<Result<FileModel>> RestoreAsync(FileId id, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<Result> DetachAsync(FileId id, FileReference reference, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<Result<Uri>> GetPublicUrlAsync(
        FileId id,
        int? width = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    private static FileModel NewModel(
        byte[] bytes,
        string name,
        string? appId,
        string contentType,
        FileVisibility visibility)
    {
        return new()
        {
            Id = FileId.New(),
            TenantId = "ocharo",
            AppId = appId,
            Name = name,
            ContentType = contentType,
            Kind = contentType.StartsWith("image/", StringComparison.Ordinal) ? FileKind.Image : FileKind.Document,
            Size = bytes.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            Visibility = visibility,
            State = FileState.Ready,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
