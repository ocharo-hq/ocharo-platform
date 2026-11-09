using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Validators;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.FoundationServices;

/// <summary>Provides the save of a catalogue design: a validated snapshot that references existing products and their
/// own images, stored as a new revision.</summary>
public sealed class DesignSaveService(
    IDesignRepository designs,
    ICatalogueRepository products,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Saves <paramref name="input"/> as a new design when <paramref name="id"/> is null, else over it.
    /// </summary>
    /// <param name="id">The design to replace; null for a new one.</param>
    /// <param name="input">The design fields.</param>
    /// <param name="ct">The token that cancels the write.</param>
    public async Task<AppResult<CatalogueDesignDetail>> SaveAsync(
        Guid? id,
        CatalogueDesignInput input,
        CancellationToken ct)
    {
        var snapshot = CatalogueDesignValidator.Validate(input);
        var definition = CatalogueDesignValidator.Definition(snapshot);
        var references = CatalogueDesignValidator.References(snapshot);
        var known = await products.GetProductsAsync(ct);
        if (!references.All(reference => known.Any(product => product.Id == reference)))
        {
            var error = CatalogueErrors.Invalid("Catalogue pages must reference existing products.");
            return AppResult<CatalogueDesignDetail>.Fail(error);
        }

        foreach (var hash in CatalogueDesignValidator.ImageHashes(snapshot))
        {
            var owned = await products.FindMediaAsync(hash, ct) is not null
                && known.Any(
                    row => CatalogueJson.Read<ProductDocument>(row.Json).Media.Any(image => image.Sha256 == hash));
            if (!owned)
            {
                var error = CatalogueErrors.Invalid("Catalogue images must belong to the product media library.");
                return AppResult<CatalogueDesignDetail>.Fail(error);
            }
        }

        if (id is not null && input.ExpectedRevision is null)
        {
            return AppResult<CatalogueDesignDetail>.Fail(
                CatalogueErrors.Invalid("Send the catalogue revision you edited."));
        }

        var now = timeProvider.GetUtcNow();
        var record = new CatalogueDesignRecord
        {
            Id = id ?? Guid.NewGuid(),
            Title = input.Title.Trim(),
            Slug = definition.Slug,
            Locale = snapshot.GetProperty("locale").GetString()!,
            Json = snapshot.GetRawText(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var saved = await designs.SaveAsync(
            record,
            id is null ? null : input.ExpectedRevision,
            ct);
        return AppResult<CatalogueDesignDetail>.Ok(DesignProjections.ToDetail(saved));
    }
}
