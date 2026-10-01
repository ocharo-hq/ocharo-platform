namespace OcharoCatalogue.Application.Catalogue.Models;

/// <summary>What an import run did, product by product.</summary>
public sealed record ImportReport(
    string SourceRoot,
    int Scanned,
    int Created,
    int Updated,
    int Unchanged,
    int ImagesStored,
    IReadOnlyList<string> Warnings);

/// <summary>An import request; <see cref="SourceRoot"/> defaults to the configured workspace products folder.</summary>
public sealed record ImportInput(string? SourceRoot, bool UpdateExisting = false, bool IncludeReferenceImages = false);
