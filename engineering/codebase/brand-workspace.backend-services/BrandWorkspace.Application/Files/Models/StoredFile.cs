namespace BrandWorkspace.Application.Files.Models;

/// <summary>A file Desk holds in WoW2 Files.</summary>
public sealed record StoredFile(string Id, string Sha256, long Size);
