namespace OcharoCatalogue.Application.Catalogue.Importing.Models;

/// <summary>Represents what an import run did, product by product.</summary>
public sealed record ImportReport(
    string SourceRoot,
    int Scanned,
    int Created,
    int Updated,
    int Unchanged,
    int ImagesStored,
    IReadOnlyList<string> Warnings);
