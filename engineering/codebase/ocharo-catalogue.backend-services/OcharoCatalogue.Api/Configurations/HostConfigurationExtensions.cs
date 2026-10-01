using OcharoCatalogue.Application;
using OcharoCatalogue.Infrastructure;
using OcharoCatalogue.Persistence;

namespace OcharoCatalogue.Api.Configurations;

/// <summary>Per-concern host registration extensions for the OcharoCatalogue API.</summary>
public static class HostConfigurationExtensions
{
    /// <summary>Configures the persistence layer (EF Core SQLite context + repository).</summary>
    public static WebApplicationBuilder AddPersistenceLayer(this WebApplicationBuilder builder)
    {
        builder.Services.AddPersistence(builder.Configuration);
        return builder;
    }

    /// <summary>Configures the infrastructure layer (clock, media store, workspace importer).</summary>
    public static WebApplicationBuilder AddInfrastructureLayer(this WebApplicationBuilder builder)
    {
        builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);
        return builder;
    }

    /// <summary>Configures the application layer (catalogue services + kit mediator).</summary>
    public static WebApplicationBuilder AddApplicationLayer(this WebApplicationBuilder builder)
    {
        builder.Services.AddApplication();
        return builder;
    }

    /// <summary>Configures API services — controllers and OpenAPI.</summary>
    public static WebApplicationBuilder AddApiServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        return builder;
    }
}
