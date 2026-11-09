using BrandWorkspace.Application.Files.Models;

namespace BrandWorkspace.Application.Files.Repositories;

/// <summary>Finds the rows stored before Files and records the files the migration moved for them. Rows of the immutable
/// <c>preparation.media</c> and <c>preparation.exports</c> never change: their file id is copied into an insert-only record
/// keyed like the row, and every read repoints through it (migration 012).</summary>
public interface IDeskFileIdRepository
{
    Task<IReadOnlyList<DeskFileWaiting>> GetWaitingAsync(CancellationToken token);
    Task FillAsync(DeskFileWaiting row, string fileId, CancellationToken token);
}
