namespace OcharoCatalogue.Application.Catalogue.Importing.Models;

/// <summary>
/// Represents an import request; <see cref="SourceRoot"/> defaults to the configured workspace products folder.
/// </summary>
public sealed record ImportInput(
    string? SourceRoot,
    bool UpdateExisting = false,
    bool IncludeReferenceImages = false);
