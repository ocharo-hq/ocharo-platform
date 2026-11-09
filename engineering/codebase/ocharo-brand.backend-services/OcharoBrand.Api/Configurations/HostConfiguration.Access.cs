using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Npgsql;
using OcharoBrand.Api.Access;
using OcharoBrand.Api.Access.Extensions;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Persistence;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;
using WoW.Two.Sdk.Beta.Be.Identity.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;

namespace OcharoBrand.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers sign-in through Ocharo's Identity instance (BFF, section <c>Identity:Client</c>), default deny, the
    /// bound organization (<c>Brand:OrganizationId</c>) and the Data Protection key ring that encrypts the sessions, both
    /// kept in the product database, so a restart or deploy keeps everyone signed in.</summary>
    /// <remarks>The BFF keeps sessions in <c>IDistributedCache</c>: the be sdk's PostgreSQL cache in schema
    /// <c>brand_cache</c>, apart from Desk's <c>cache</c> and Studio's <c>studio_cache</c>, its <c>distributed_cache</c> set
    /// migrating beside Brand's own. The key ring lives in <c>brand.data_protection_keys</c> under today's application name,
    /// and <c>import-sqlite</c> copies the file ring into it.</remarks>
    private static WebApplicationBuilder AddAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentityBff();
        builder.Services.AddPostgresDistributedCache(options => options.Schema = BrandDatabaseConstants.CacheSchema);
        builder.Services.AddOptions<PostgresDistributedCacheOptions>().Configure<IConfiguration>((options, configuration) =>
            options.ConnectionString = configuration.GetConnectionString(BrandDatabaseConstants.ConnectionStringName) ?? string.Empty);
        builder.Services.AddDefaultDenyAuthorization(BffAuthenticationSchemeConstants.Cookie);
        builder.Services.AddOptions<BrandAccessOptions>().BindConfiguration(BrandAccessOptions.Section)
            .Validate(options => options.OrganizationId != Guid.Empty, "Brand:OrganizationId must name the Ocharo organization Brand serves.")
            .ValidateOnStart();
        // Today's application name, unchanged, so a cookie issued before the move stays readable once its key is copied.
        builder.Services.AddDataProtection();
        builder.Services.AddOptions<KeyManagementOptions>().Configure<NpgsqlDataSource>((options, dataSource) =>
            options.XmlRepository = new PostgresXmlRepository(dataSource));
        builder.Services.AddOptions<DataProtectionOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            options.ApplicationDiscriminator = configuration["DataProtection:ApplicationName"] ?? $"ocharo-brand:{environment.EnvironmentName}");
        return builder;
    }

    /// <summary>Configures the session pipeline after routing: HTTPS for the session routes, the BFF (authentication, the
    /// <c>X-Csrf-Token</c> check, authorization), then the bound-organization membership and permission check.</summary>
    private static IApplicationBuilder UseAccess(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/identity") && !context.Request.IsHttps)
            {
                await context.WriteProblemAsync(BrandErrors.HttpsRequired());
                return;
            }
            await next(context);
        });
        app.UseIdentityBff();
        app.UseMiddleware<BrandAccessMiddleware>();
        return app;
    }
}
