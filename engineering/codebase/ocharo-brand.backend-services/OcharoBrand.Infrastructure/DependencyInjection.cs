using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OcharoBrand.Application.Abstractions;
using OcharoBrand.Application.Brand;
using OcharoBrand.Infrastructure.Brand;
using OcharoBrand.Infrastructure.Time;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using WoW.Two.Sdk.Backend.Beta.Storage.FileSystem;

namespace OcharoBrand.Infrastructure;

/// <summary>Registers infrastructure: clock, blob-backed media store and the brand seeder.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddOptions<BrandOptions>().Bind(configuration.GetSection(BrandOptions.Section));
        // Resolved lazily so configuration overrides applied after registration (tests, hosts) still win.
        services.AddSingleton<IBlobRepository>(provider =>
        {
            var dataRoot = provider.GetRequiredService<IOptions<BrandOptions>>().Value.DataRoot;
            var root = Path.IsPathRooted(dataRoot) ? dataRoot : Path.Combine(contentRoot, dataRoot);
            return new LocalFileBlobRepository(Path.Combine(root, "blobs"));
        });
        services.AddScoped<IMediaStore, MediaStore>();
        services.AddScoped<BrandSeeder>();
        return services;
    }
}
