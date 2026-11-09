using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.CommandHandlers;

/// <summary>Handles <see cref="ProductPricingBulkAdjustCommand"/>.</summary>
public sealed class ProductPricingBulkAdjustCommandHandler(ICatalogueRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ProductPricingBulkAdjustCommand, AppResult<BulkResult>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<BulkResult>> HandleAsync(
        ProductPricingBulkAdjustCommand request,
        CancellationToken ct)
    {
        var input = request.Input;
        if (Problem(input) is { } problem)
        {
            return AppResult<BulkResult>.Fail(problem);
        }

        var skipped = new List<string>();
        var changed = 0;
        foreach (var id in input.ProductIds.Distinct())
        {
            var record = await repository.FindProductAsync(id, ct);
            if (record is null)
            {
                skipped.Add($"{id}: not found");
                continue;
            }

            var document = CatalogueJson.Read<ProductDocument>(record.Json);
            if (document.Pricing is not { Tiers.Count: > 0 } pricing)
            {
                skipped.Add($"{id}: no price recorded");
                continue;
            }

            if (!ProductPublicationEligibility.StandardBands(pricing.Tiers))
            {
                skipped.Add($"{id}: confirm the 1–9 and 10+ price bands first");
                continue;
            }

            var bulkStart = pricing.Tiers.Max(tier => tier.MinimumQuantity);
            var tiers = pricing.Tiers
                .Select(tier => Adjust(
                    tier,
                    input,
                    bulkStart))
                .ToList();
            if (!ProductPublicationEligibility.ValidTiers(tiers))
            {
                skipped.Add($"{id}: adjusted price is out of range");
                continue;
            }

            if (tiers.SequenceEqual(pricing.Tiers))
            {
                continue;
            }

            var next = document with { Pricing = pricing with { Tiers = tiers, Status = PricingStatus.Draft } };
            var summary = input.Mode == "percent"
                ? $"Prices {input.Value:+0.##;-0.##}%"
                : $"Prices {input.Value:+0;-0} UZS";
            await repository.UpdateProductAsync(
                ProductProjections.ToRecord(
                    next,
                    record.Revision + 1,
                    record.CreatedAt,
                    timeProvider.GetUtcNow()),
                record.Revision,
                summary,
                ct);
            changed++;
        }

        return AppResult<BulkResult>.Ok(new BulkResult(changed, skipped));
    }

    private static AppError? Problem(BulkPricingInput input)
    {
        if (input.Mode is not ("percent" or "add"))
        {
            return CatalogueErrors.Invalid("Mode must be 'percent' or 'add'.");
        }

        if (input.RoundTo is < 1 or > 1_000_000)
        {
            return CatalogueErrors.Invalid("Rounding must be between 1 and 1,000,000.");
        }

        if (input.Mode == "percent" && input.Value is < -90 or > 500)
        {
            return CatalogueErrors.Invalid("Percent change must be between -90 and 500.");
        }

        if (input.Tier is not (null or "retail" or "wholesale"))
        {
            return CatalogueErrors.Invalid("Tier must be retail, wholesale or empty for all.");
        }

        return null;
    }

    private static PriceTier Adjust(
        PriceTier tier,
        BulkPricingInput input,
        int bulkStart)
    {
        var applies = input.Tier switch
        {
            "retail" => tier.MinimumQuantity == 1,
            "wholesale" => tier.MinimumQuantity == bulkStart && bulkStart > 1,
            _ => true,
        };
        if (!applies)
        {
            return tier;
        }

        var raw = input.Mode == "percent" ? tier.UnitPrice * (100 + input.Value) / 100 : tier.UnitPrice + input.Value;
        var rounded = (long)(Math.Round(raw / input.RoundTo, MidpointRounding.AwayFromZero) * input.RoundTo);
        return tier with { UnitPrice = Math.Max(0, rounded) };
    }
}
