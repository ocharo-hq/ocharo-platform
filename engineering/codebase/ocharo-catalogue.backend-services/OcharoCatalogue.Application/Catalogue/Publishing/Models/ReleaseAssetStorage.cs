using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents where a release's artifacts went.</summary>
/// <param name="Rows">The rows whose bytes SQLite keeps.</param>
/// <param name="Files">The files WoW2 Files holds.</param>
public sealed record ReleaseAssetStorage(
    IReadOnlyList<CatalogueReleaseAssetRecord> Rows,
    IReadOnlyList<CatalogueReleaseFileRecord> Files);
