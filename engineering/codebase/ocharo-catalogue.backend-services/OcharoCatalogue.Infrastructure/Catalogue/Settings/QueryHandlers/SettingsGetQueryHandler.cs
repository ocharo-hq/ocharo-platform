using Microsoft.Extensions.Options;
using OcharoCatalogue.Application.Catalogue.Settings.Models;
using OcharoCatalogue.Application.Catalogue.Settings.Queries;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Options;
using OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Settings.QueryHandlers;

/// <summary>Handles <see cref="SettingsGetQuery"/>.</summary>
public sealed class SettingsGetQueryHandler(CatalogueSettingsService settings, IOptions<CatalogueOptions> options)
    : IQueryHandler<SettingsGetQuery, AppResult<CatalogueSettingsDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CatalogueSettingsDetail>> HandleAsync(
        SettingsGetQuery request,
        CancellationToken ct)
    {
        var (value, revision) = await settings.GetWithRevisionAsync(ct);
        return AppResult<CatalogueSettingsDetail>.Ok(new CatalogueSettingsDetail(
            value,
            revision,
            options.Value.SourceRoot));
    }
}
