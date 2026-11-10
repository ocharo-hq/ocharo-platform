using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Npgsql;
using OcharoEvents.Api.Access.Extensions;
using OcharoEvents.Application.Events.Errors;
using OcharoEvents.Persistence;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;
using WoW.Two.Sdk.Beta.Be.Identity.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;

namespace OcharoEvents.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>The Data Protection application name a host without <c>DataProtection:ApplicationName</c> uses, the
    /// descriptor's default too.</summary>
    private const string DataProtectionApplicationName = "ocharo-events";

    /// <summary>Registers sign-in through Ocharo's Identity instance (BFF, section <c>Identity:Client</c>), default deny and
    /// the Data Protection key ring that encrypts the sessions, both kept in the product database, so a restart or deploy
    /// keeps everyone signed in (events-app.md #2, #4).</summary>
    /// <remarks>The BFF keeps sessions in <c>IDistributedCache</c>: the be sdk's PostgreSQL cache in schema
    /// <c>ocharo_events_cache</c>, apart from Desk's <c>cache</c>, Brand's <c>brand_cache</c> and Catalogue's
    /// <c>catalogue_cache</c>, its <c>distributed_cache</c> set migrating beside the service's own. The key ring lives in
    /// <c>ocharo_events.data_protection_keys</c>.</remarks>
    private static WebApplicationBuilder AddAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentityBff();
        builder.Services.AddPostgresDistributedCache(options => options.Schema = EventsDatabaseConstants.CacheSchema);
        builder.Services.AddOptions<PostgresDistributedCacheOptions>().Configure<IConfiguration>((options, configuration) =>
            options.ConnectionString = configuration.GetConnectionString(EventsDatabaseConstants.ConnectionStringName) ?? string.Empty);
        builder.Services.AddDefaultDenyAuthorization(BffAuthenticationSchemeConstants.Cookie);
        builder.Services.AddDataProtection();
        builder.Services.AddOptions<KeyManagementOptions>().Configure<NpgsqlDataSource>((options, dataSource) =>
            options.XmlRepository = new PostgresXmlRepository(dataSource));
        builder.Services.AddOptions<DataProtectionOptions>().Configure<IConfiguration>((options, configuration) =>
            options.ApplicationDiscriminator = configuration["DataProtection:ApplicationName"] is { Length: > 0 } name
                ? name
                : DataProtectionApplicationName);
        return builder;
    }

    /// <summary>Configures the session pipeline after routing: the session and organization routes answer uncached and
    /// only over HTTPS, as Desk's did; then the BFF (authentication, the <c>X-Csrf-Token</c> check, authorization). The
    /// organization access filter on the Events controller checks the route's organization.</summary>
    private static IApplicationBuilder UseAccess(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/api/identity") || path.StartsWithSegments("/api/organizations"))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "no-store";
                    context.Response.Headers.Pragma = "no-cache";
                    return Task.CompletedTask;
                });
                if (!context.Request.IsHttps)
                {
                    await context.WriteProblemAsync(OcharoEventsErrors.HttpsRequired());
                    return;
                }
            }
            await next(context);
        });
        app.UseIdentityBff();
        return app;
    }
}
