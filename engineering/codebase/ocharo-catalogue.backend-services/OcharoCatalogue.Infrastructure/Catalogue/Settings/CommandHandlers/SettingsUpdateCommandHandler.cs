using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Application.Catalogue.Core.Constants;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Settings.Commands;
using OcharoCatalogue.Application.Catalogue.Settings.Models;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Options;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Settings.CommandHandlers;

/// <summary>Handles <see cref="SettingsUpdateCommand"/>.</summary>
public sealed partial class SettingsUpdateCommandHandler(
    ICatalogueRepository repository,
    IOptions<CatalogueOptions> options)
    : ICommandHandler<SettingsUpdateCommand, AppResult<CatalogueSettingsDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CatalogueSettingsDetail>> HandleAsync(
        SettingsUpdateCommand request,
        CancellationToken ct)
    {
        var settings = request.Settings;
        if (Problem(settings) is { } problem)
        {
            return AppResult<CatalogueSettingsDetail>.Fail(problem);
        }

        var existing = await repository.GetProductsAsync(ct);
        var used = existing
            .Select(product => product.GarmentType)
            .Distinct()
            .Where(key => settings.GarmentTypes.All(garment => garment.Key != key))
            .ToList();
        if (used.Count > 0)
        {
            var error = CatalogueErrors.Invalid($"Garment types still used by products: {string.Join(", ", used)}.");
            return AppResult<CatalogueSettingsDetail>.Fail(error);
        }

        var normalized = settings with
        {
            BrandName = settings.BrandName.Trim(),
            SizePreset = settings.SizePreset
                .Select(size => size.Trim())
                .Where(size => size.Length is > 0 and <= 12)
                .Distinct()
                .ToList(),
            ContactLines = settings.ContactLines
                .Select(line => line.Trim())
                .Where(line => line.Length is > 0 and <= 200)
                .Take(8)
                .ToList(),
            PriceNote = string.IsNullOrWhiteSpace(settings.PriceNote) ? null : settings.PriceNote.Trim(),
        };
        var record = new SettingsRecord
        {
            Id = CatalogueKeys.SettingsId,
            Revision = request.ExpectedRevision + 1,
            Json = CatalogueJson.Write(normalized),
        };
        var saved = await repository.SaveSettingsAsync(
            record,
            request.ExpectedRevision == 0 ? null : request.ExpectedRevision,
            ct);
        var detail = new CatalogueSettingsDetail(
            normalized,
            saved.Revision,
            options.Value.SourceRoot);
        return AppResult<CatalogueSettingsDetail>.Ok(detail);
    }

    private static AppError? Problem(CatalogueSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BrandName) || settings.BrandName.Length > 80)
        {
            return CatalogueErrors.Invalid("Brand name is required (max 80).");
        }

        if (settings.Currency != "UZS")
        {
            return CatalogueErrors.Invalid("Only UZS pricing is supported in this version.");
        }

        if (settings.GarmentTypes.Count == 0)
        {
            return CatalogueErrors.Invalid("Keep at least one garment type.");
        }

        if (settings.GarmentTypes.Select(garment => garment.Key).Distinct().Count() != settings.GarmentTypes.Count)
        {
            return CatalogueErrors.Invalid("Garment type keys must be unique.");
        }

        foreach (var type in settings.GarmentTypes)
        {
            if (!Key().IsMatch(type.Key))
            {
                return CatalogueErrors.Invalid($"Garment key '{type.Key}' must be a lowercase slug.");
            }

            if (string.IsNullOrWhiteSpace(type.Uz))
            {
                return CatalogueErrors.Invalid($"Garment '{type.Key}' needs an Uzbek name.");
            }
        }

        return null;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Key();
}
