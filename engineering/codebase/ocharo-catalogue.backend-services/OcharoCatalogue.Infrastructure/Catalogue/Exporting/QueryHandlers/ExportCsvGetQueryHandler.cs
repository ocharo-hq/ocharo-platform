using System.Globalization;
using System.Text;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Exporting.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Exporting.QueryHandlers;

/// <summary>Handles <see cref="ExportCsvGetQuery"/>.</summary>
public sealed class ExportCsvGetQueryHandler(ICatalogueRepository repository)
    : IQueryHandler<ExportCsvGetQuery, AppResult<byte[]>>
{
    private const string Header =
        "id,garment_type,variant,status,name_uz,name_ru,name_en,sizes,retail_uzs,wholesale_uzs,wholesale_from_qty,"
        + "pricing_status,open_issues";

    /// <inheritdoc />
    public async ValueTask<AppResult<byte[]>> HandleAsync(ExportCsvGetQuery request, CancellationToken ct)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Header);
        var records = (await repository.GetProductsAsync(ct))
            .OrderBy(record => record.SortOrder)
            .ThenBy(record => record.Id, StringComparer.Ordinal);
        foreach (var record in records)
        {
            builder.AppendJoin(',', Cells(CatalogueJson.Read<ProductDocument>(record.Json)).Select(Csv));
            builder.AppendLine();
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        return AppResult<byte[]>.Ok(bytes);
    }

    private static string?[] Cells(ProductDocument document)
    {
        var bulk = document.Pricing?.Tiers
            .OrderByDescending(tier => tier.MinimumQuantity)
            .FirstOrDefault(tier => tier.MinimumQuantity > 1);
        return
        [
            document.Id,
            document.GarmentType,
            document.VariantCode,
            document.Status,
            document.Locales.Uz.Name,
            document.Locales.Ru.Name,
            document.Locales.En.Name,
            string.Join(' ', document.Sizes),
            document.Pricing?.RetailPrice?.ToString(CultureInfo.InvariantCulture),
            bulk?.UnitPrice.ToString(CultureInfo.InvariantCulture),
            bulk?.MinimumQuantity.ToString(CultureInfo.InvariantCulture),
            document.Pricing?.Status,
            document.Issues.Count(issue => issue.Status == IssueStatus.Open).ToString(CultureInfo.InvariantCulture),
        ];
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        // Neutralize spreadsheet formula injection and quote separators.
        var safe = value[0] is '=' or '+' or '-' or '@' ? "'" + value : value;
        return safe.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{safe.Replace("\"", "\"\"")}\"" : safe;
    }
}
