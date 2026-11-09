using BrandWorkspace.Application.Foundation.Errors;
using System.Security.Cryptography;
using BrandWorkspace.Application.Files.Constants;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Files.Services;
using BrandWorkspace.Application.Files.Repositories;
using BrandWorkspace.Application.Preparation.Models;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Beta.Be.Files;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Infrastructure.Files.Services;

/// <summary>
/// Fills the file id of each row stored before Files from the files the migration moved (Files t9 #5 step 6): every
/// <c>desk</c> file in Files whose SHA-256 matches a waiting row and that carries that row's own reference, so a file
/// never crosses an organization, brand or record. Runs at boot as Desk itself; a row the migration has not moved yet
/// keeps reading its PostgreSQL bytes.
/// </summary>
public sealed class DeskFilesBackfill(
    IDeskFileIdRepository rows,
    IFileStoreBroker files,
    IFileReferencesBroker references,
    ILogger<DeskFilesBackfill> logger)
{
    public async Task RunAsync(CancellationToken token)
    {
        var waiting = (await rows.GetWaitingAsync(token))
            .GroupBy(row => row.Sha256, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        if (waiting.Count == 0) return;

        var filled = 0;
        string? cursor = null;
        do
        {
            var page = await files.ListAsync(
                new FileQuery { AppId = DeskFiles.AppId, Cursor = cursor, Limit = FilesConstants.MaxPageSize },
                token);
            if (page is Result<FilePage>.Failure failure)
            {
                logger.LogWarning(
                    "Desk's file ids stay unfilled: WoW2 Files answered {Type}: {Message}",
                    failure.Error.Type,
                    failure.Error.Message);
                return;
            }
            var (items, next) = page is Result<FilePage>.Success success
                ? (success.Value.Items, success.Value.NextCursor)
                : ([], null);
            foreach (var file in items)
            {
                if (file.State != FileState.Ready || !waiting.TryGetValue(file.Sha256, out var candidates)) continue;
                var listed = await references.ListAsync(file.Id, token);
                if (listed is not Result<IReadOnlyList<FileReference>>.Success attached) continue;
                foreach (var row in candidates
                    .Where(row =>
                        attached.Value.Contains(new FileReference(row.Reference.RecordType, row.Reference.RecordId)))
                    .ToList())
                {
                    await rows.FillAsync(row, file.Id.ToString(), token);
                    candidates.Remove(row);
                    filled++;
                }
                if (candidates.Count == 0) waiting.Remove(file.Sha256);
            }
            cursor = next;
        }
        while (cursor is not null && waiting.Count > 0);

        logger.LogInformation(
            "Desk filled {Filled} file ids from WoW2 Files; {Waiting} rows still read their PostgreSQL bytes.",
            filled,
            waiting.Values.Sum(candidates => candidates.Count));
    }
}
