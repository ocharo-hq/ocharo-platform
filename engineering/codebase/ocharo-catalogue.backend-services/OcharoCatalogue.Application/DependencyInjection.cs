using Microsoft.Extensions.DependencyInjection;
using OcharoCatalogue.Application.Catalogue.Services;
using WoW.Two.Sdk.Backend.Beta.Mediator;

namespace OcharoCatalogue.Application;

/// <summary>Registers application-layer services — catalogue commands/queries and the kit mediator.</summary>
public static class DependencyInjection
{
    /// <summary>Adds the catalogue services and scans this assembly for kit request handlers.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(typeof(DependencyInjection).Assembly);
        services.AddScoped<SettingsService>();
        services.AddScoped<ProductService>();
        services.AddScoped<CollectionService>();
        services.AddScoped<PublicCatalogueService>();
        services.AddScoped<CatalogueExportService>();
        return services;
    }
}
