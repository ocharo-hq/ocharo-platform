using Microsoft.Extensions.DependencyInjection;
using OcharoBrand.Application.Brand;
using WoW.Two.Sdk.Backend.Beta.Mediator;

namespace OcharoBrand.Application;

/// <summary>Registers application-layer services — brand draft/release/appearance services and the public kit.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(typeof(DependencyInjection).Assembly);
        services.AddScoped<BrandService>();
        services.AddScoped<PublicBrandService>();
        return services;
    }
}
