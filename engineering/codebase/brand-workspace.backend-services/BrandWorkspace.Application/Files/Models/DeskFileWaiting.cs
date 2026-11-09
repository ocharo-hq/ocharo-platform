namespace BrandWorkspace.Application.Files.Models;

/// <summary>
/// A row stored before Files that still reads its PostgreSQL bytes, and the reference its moved file carries.
/// </summary>
public sealed record DeskFileWaiting(DeskFileReference Reference, string Sha256);
