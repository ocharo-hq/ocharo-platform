using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Files.Services;

namespace BrandWorkspace.Infrastructure.Files.Services;

/// <summary>The store of a run without WoW2 Files: every byte stays in PostgreSQL.</summary>
public sealed class NoDeskFiles : IDeskFileStore
{
    public static readonly NoDeskFiles Instance = new();

    public bool IsEnabled => false;

    public Task<StoredFile> StoreAsync(byte[] bytes, string name, string contentType, DeskFileReference reference, CancellationToken token) =>
        throw Disabled();

    public Task AttachAsync(string fileId, DeskFileReference reference, CancellationToken token) => throw Disabled();

    public Task<Uri> LinkAsync(string fileId, int? width, bool download, CancellationToken token) => throw Disabled();

    public Task<byte[]> ReadAsync(string fileId, string sha256, CancellationToken token) => throw Disabled();

    private static InvalidOperationException Disabled() =>
        new("WoW2 Files is not configured (Files:BaseUrl), yet a stored row names a Files file.");
}
