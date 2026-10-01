using System.Text.RegularExpressions;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Named, ordered product selections (season lines, lookbooks, merchant sheets).</summary>
public sealed partial class CollectionService(ICatalogueRepository repository, IClock clock)
{
    public async Task<IReadOnlyList<CollectionDetail>> ListAsync(CancellationToken ct) =>
        (await repository.GetCollectionsAsync(ct))
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(Detail)
            .ToList();

    public async Task<CollectionDetail> GetAsync(string id, CancellationToken ct) =>
        Detail(await repository.FindCollectionAsync(id, ct) ?? throw CatalogueException.NotFound("Collection"));

    public async Task<CollectionDetail> SaveAsync(string? id, CollectionInput input, CancellationToken ct)
    {
        var creating = id is null;
        id ??= string.IsNullOrWhiteSpace(input.Id) ? Slugify(input.Names.Uz) : input.Id.Trim();
        if (!Slug().IsMatch(id) || id.Length > 64) throw new CatalogueException("Collection id must be a lowercase slug (max 64).");
        if (string.IsNullOrWhiteSpace(input.Names.Uz) || input.Names.Uz.Length > 120) throw new CatalogueException("A collection needs an Uzbek name (max 120).");
        var existing = await repository.FindCollectionAsync(id, ct);
        if (creating && existing is not null) throw CatalogueException.Conflict($"Collection '{id}' already exists.");
        if (!creating && existing is null) throw CatalogueException.NotFound("Collection");
        if (!creating && input.ExpectedRevision is null) throw new CatalogueException("Send the revision you edited (expectedRevision).");

        var known = (await repository.GetProductsAsync(ct)).Select(p => p.Id).ToHashSet();
        var productIds = (input.ProductIds ?? []).Distinct().ToList();
        var unknown = productIds.Where(p => !known.Contains(p)).ToList();
        if (unknown.Count > 0) throw new CatalogueException($"Unknown products: {string.Join(", ", unknown)}.");

        var document = new CollectionDocument
        {
            Id = id,
            Names = new CollectionNames(input.Names.Uz.Trim(), Trim(input.Names.Ru), Trim(input.Names.En)),
            Description = Trim(input.Description),
            ProductIds = productIds,
            IsPublished = input.IsPublished,
            SortOrder = input.SortOrder,
        };
        var record = new CollectionRecord
        {
            Id = id,
            SortOrder = input.SortOrder,
            Revision = (existing?.Revision ?? 0) + 1,
            UpdatedAt = clock.UtcNow,
            Json = CatalogueJson.Write(document),
        };
        return Detail(await repository.SaveCollectionAsync(record, creating ? null : input.ExpectedRevision, ct));
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        _ = await repository.FindCollectionAsync(id, ct) ?? throw CatalogueException.NotFound("Collection");
        await repository.DeleteCollectionAsync(id, ct);
    }

    private static CollectionDetail Detail(CollectionRecord r) => new(CatalogueJson.Read<CollectionDocument>(r.Json), r.Revision, r.UpdatedAt);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Slugify(string name)
    {
        var map = new Dictionary<char, string> { ['‘'] = "", ['’'] = "", ['\''] = "", ['ʻ'] = "", ['ʼ'] = "" };
        var chars = name.Trim().ToLowerInvariant().Select(c => map.TryGetValue(c, out var r) ? r : char.IsAsciiLetterOrDigit(c) ? c.ToString() : "-");
        var slug = Regex.Replace(string.Concat(chars), "-+", "-").Trim('-');
        return slug.Length == 0 ? $"collection-{Guid.NewGuid():N}"[..20] : slug[..Math.Min(slug.Length, 64)].Trim('-');
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Slug();
}
