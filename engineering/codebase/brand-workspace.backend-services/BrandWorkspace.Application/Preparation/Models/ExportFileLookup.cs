namespace BrandWorkspace.Application.Preparation.Models;

/// <summary>Represents the WoW2 Files file holding an export's ZIP, or none while PostgreSQL alone holds it.</summary>
/// <param name="FileId">The file's id, if Files holds the ZIP.</param>
public sealed record ExportFileLookup(string? FileId);
