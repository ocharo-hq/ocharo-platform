using System.Text.RegularExpressions;
using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Collections.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Collections.FoundationServices;

/// <summary>Provides the save of a named, ordered product selection (season lines, lookbooks, merchant sheets): a new
/// one
/// without an id, an existing one at the revision the caller edited.</summary>
public sealed partial class CollectionSaveService(ICatalogueRepository repository, TimeProvider timeProvider)
{
    /// <summary>
    /// Saves <paramref name="input"/> as a new collection when <paramref name="id"/> is null, else over it.
    /// </summary>
    /// <param name="id">The collection to replace; null for a new one.</param>
    /// <param name="input">The collection fields.</param>
    /// <param name="ct">The token that cancels the write.</param>
    public async Task<AppResult<CollectionDetail>> SaveAsync(
        string? id,
        CollectionInput input,
        CancellationToken ct)
    {
        var creating = id is null;
        id ??= string.IsNullOrWhiteSpace(input.Id) ? Slugify(input.Names.Uz) : input.Id.Trim();
        if (!Slug().IsMatch(id) || id.Length > 64)
        {
            return AppResult<CollectionDetail>.Fail(
                CatalogueErrors.Invalid("Collection id must be a lowercase slug (max 64)."));
        }

        if (string.IsNullOrWhiteSpace(input.Names.Uz) || input.Names.Uz.Length > 120)
        {
            return AppResult<CollectionDetail>.Fail(
                CatalogueErrors.Invalid("A collection needs an Uzbek name (max 120)."));
        }

        var existing = await repository.FindCollectionAsync(id, ct);
        if (creating && existing is not null)
        {
            return AppResult<CollectionDetail>.Fail(CatalogueErrors.Conflict($"Collection '{id}' already exists."));
        }

        if (!creating && existing is null)
        {
            return AppResult<CollectionDetail>.Fail(CatalogueErrors.NotFound("Collection"));
        }

        if (!creating && input.ExpectedRevision is null)
        {
            return AppResult<CollectionDetail>.Fail(
                CatalogueErrors.Invalid("Send the revision you edited (expectedRevision)."));
        }

        var known = (await repository.GetProductsAsync(ct)).Select(product => product.Id).ToHashSet();
        var productIds = (input.ProductIds ?? []).Distinct().ToList();
        var unknown = productIds.Where(productId => !known.Contains(productId)).ToList();
        if (unknown.Count > 0)
        {
            return AppResult<CollectionDetail>.Fail(
                CatalogueErrors.Invalid($"Unknown products: {string.Join(", ", unknown)}."));
        }

        var document = new CollectionDocument
        {
            Id = id,
            Names = new CollectionNames(
                input.Names.Uz.Trim(),
                Trim(input.Names.Ru),
                Trim(input.Names.En)),
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
            UpdatedAt = timeProvider.GetUtcNow(),
            Json = CatalogueJson.Write(document),
        };
        var saved = await repository.SaveCollectionAsync(
            record,
            creating ? null : input.ExpectedRevision,
            ct);
        return AppResult<CollectionDetail>.Ok(CollectionDetails.ToDetail(saved));
    }

    private static string? Trim(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Slugify(string name)
    {
        var map = new Dictionary<char, string> { ['‘'] = "", ['’'] = "", ['\''] = "", ['ʻ'] = "", ['ʼ'] = "" };
        var chars = name.Trim().ToLowerInvariant().Select(character => Slugged(character, map));
        var slug = Regex.Replace(
                string.Concat(chars),
                "-+",
                "-")
            .Trim('-');
        if (slug.Length == 0)
        {
            return $"collection-{Guid.NewGuid():N}"[..20];
        }

        return slug[..Math.Min(slug.Length, 64)].Trim('-');
    }

    private static string Slugged(char character, IReadOnlyDictionary<char, string> map)
    {
        if (map.TryGetValue(character, out var replacement))
        {
            return replacement;
        }

        return char.IsAsciiLetterOrDigit(character) ? character.ToString() : "-";
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Slug();
}
