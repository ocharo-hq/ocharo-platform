using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Infrastructure.Catalogue;
using OcharoCatalogue.Infrastructure.Time;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using WoW.Two.Sdk.Backend.Beta.Storage.FileSystem;

namespace OcharoCatalogue.Infrastructure;

/// <summary>Registers the infrastructure layer — clock, blob-backed media store and the workspace importer.</summary>
public static class DependencyInjection
{
    /// <summary>Adds infrastructure services; <paramref name="contentRoot"/> anchors a relative data root.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddOptions<CatalogueOptions>().Bind(configuration.GetSection(CatalogueOptions.Section));
        // Resolved lazily so configuration overrides applied after registration (tests, hosts) still win.
        services.AddSingleton<IBlobRepository>(provider =>
        {
            var dataRoot = provider.GetRequiredService<IOptions<CatalogueOptions>>().Value.DataRoot;
            var root = Path.IsPathRooted(dataRoot) ? dataRoot : Path.Combine(contentRoot, dataRoot);
            return new LocalFileBlobRepository(Path.Combine(root, "blobs"));
        });
        services.AddScoped<IMediaStore, MediaStore>();
        services.AddScoped<WorkspaceCatalogueImporter>();
        return services;
    }
}
