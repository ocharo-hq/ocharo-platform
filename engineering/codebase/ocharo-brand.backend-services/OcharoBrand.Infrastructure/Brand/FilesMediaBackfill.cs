using Microsoft.Extensions.Logging;
using OcharoBrand.Application.Brand.Repositories;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Files;
using WoW.Two.Sdk.Beta.Be.Files.Store;

namespace OcharoBrand.Infrastructure.Brand;

/// <summary>Fills the file id of each file stored before Files from the files the migration moved: every Brand file in
/// Files, matched by SHA-256 (Files t9 #5 step 6). Runs at boot as Brand itself; a file the migration has not moved yet
/// keeps reading from the local blob folder.</summary>
public sealed class FilesMediaBackfill(IBrandRepository repository, IFileStoreBroker files, ILogger<FilesMediaBackfill> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var waiting = (await repository.GetMediaWithoutFileAsync(ct)).Select(record => record.Sha256).ToHashSet(StringComparer.Ordinal);
        if (waiting.Count == 0) return;

        var filled = 0;
        string? cursor = null;
        do
        {
            var page = await files.ListAsync(new FileQuery { AppId = BrandFiles.AppId, Cursor = cursor, Limit = FilesConstants.MaxPageSize }, ct);
            if (page is Result<FilePage>.Failure failure)
            {
                logger.LogWarning("Brand's file ids stay unfilled: WoW2 Files answered {Type}: {Message}", failure.Error.Type, failure.Error.Message);
                return;
            }
            var (items, next) = page is Result<FilePage>.Success success ? (success.Value.Items, success.Value.NextCursor) : ([], null);
            foreach (var file in items.Where(file => waiting.Remove(file.Sha256)))
            {
                await repository.SetMediaFileAsync(file.Sha256, file.Id.ToString(), ct);
                filled++;
            }
            cursor = next;
        }
        while (cursor is not null && waiting.Count > 0);

        if (filled > 0 || waiting.Count > 0)
            logger.LogInformation("Brand filled {Filled} file ids from WoW2 Files; {Waiting} still read from the local blob folder.", filled, waiting.Count);
    }
}
