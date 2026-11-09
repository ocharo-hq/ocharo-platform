using Microsoft.Extensions.Logging;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Constants;
using WoW.Two.Sdk.Beta.Be.Files;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoCatalogue.Infrastructure.Catalogue.Media.FoundationServices;

/// <summary>Provides the boot-time fill of the file id of each image and release artifact stored before Files, from the
/// files the migration moved: every Catalogue file in Files, matched by SHA-256 (Files t9 #5 step 6). It runs as
/// Catalogue
/// itself; a file the migration has not moved yet keeps reading from the local blob folder or SQLite.</summary>
public sealed class FilesMediaBackfillService(
    ICatalogueRepository repository,
    IDesignRepository designs,
    IFileStoreBroker files,
    ILogger<FilesMediaBackfillService> logger)
{
    /// <summary>Fills every file id Files can match.</summary>
    /// <param name="ct">The token that cancels the run.</param>
    public async Task RunAsync(CancellationToken ct)
    {
        var waiting = (await repository.GetMediaWithoutFileAsync(ct))
            .Select(record => record.Sha256)
            .ToHashSet(StringComparer.Ordinal);
        var artifacts = new Dictionary<string, string>(
            await designs.AssetsWithoutFileAsync(ct),
            StringComparer.Ordinal);
        if (waiting.Count == 0 && artifacts.Count == 0)
        {
            return;
        }

        var filled = 0;
        string? cursor = null;
        do
        {
            var query = new FileQuery
            {
                AppId = CatalogueFiles.AppId,
                Cursor = cursor,
                Limit = FilesConstants.MaxPageSize,
            };
            var page = await files.ListAsync(query, ct);
            if (page is Result<FilePage>.Failure failure)
            {
                logger.LogWarning(
                    "Catalogue's file ids stay unfilled: WoW2 Files answered {Type}: {Message}",
                    failure.Error.Type,
                    failure.Error.Message);
                return;
            }

            var (items, next) = page is Result<FilePage>.Success success
                ? (success.Value.Items, success.Value.NextCursor)
                : ([], null);
            foreach (var file in items)
            {
                if (waiting.Remove(file.Sha256))
                {
                    await repository.SetMediaFileAsync(
                        file.Sha256,
                        file.Id.ToString(),
                        ct);
                    filled++;
                }

                if (artifacts.Remove(file.Sha256, out var contentType))
                {
                    var record = new CatalogueReleaseFileRecord
                    {
                        Sha256 = file.Sha256,
                        ContentType = contentType,
                        FileId = file.Id.ToString(),
                    };
                    await designs.SaveReleaseFileAsync(record, ct);
                    filled++;
                }
            }

            cursor = next;
        }
        while (cursor is not null && (waiting.Count > 0 || artifacts.Count > 0));

        if (filled > 0 || waiting.Count > 0 || artifacts.Count > 0)
        {
            logger.LogInformation(
                "Catalogue filled {Filled} file ids from WoW2 Files; {Waiting} images still read from the local "
                + "blob folder, {Artifacts} release artifacts from SQLite.",
                filled,
                waiting.Count,
                artifacts.Count);
        }
    }
}
