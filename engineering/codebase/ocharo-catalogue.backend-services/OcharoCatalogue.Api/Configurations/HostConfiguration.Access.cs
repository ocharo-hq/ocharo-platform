using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Npgsql;
using OcharoCatalogue.Api.Access;
using OcharoCatalogue.Api.Catalogue.Core.Errors;
using OcharoCatalogue.Api.Catalogue.Core.Extensions;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Options;
using OcharoCatalogue.Persistence;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;

namespace OcharoCatalogue.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>
    /// Registers sign-in through Ocharo's Identity instance (BFF, section <c>Identity:Client</c>), the bound
    /// organization (<c>Catalogue:OrganizationId</c>, required at boot), default deny with <see
    /// cref="CatalogueAccess.MemberPolicy"/> as the fallback, and the sessions and Data Protection key ring that encrypts
    /// them, both in the product database, so a restart or deploy keeps everyone signed in.
    /// </summary>
    /// <remarks>
    ///   - the BFF keeps its sessions in <c>IDistributedCache</c>: the be sdk's PostgreSQL cache in schema
    ///     <c>catalogue_cache</c>, apart from Desk's <c>cache</c> and Studio's <c>studio_cache</c>, its
    ///     <c>distributed_cache</c> set migrating beside Catalogue's own
    ///   - the key ring lives in <c>catalogue.data_protection_keys</c> under the application name Catalogue has always
    ///     used; <c>import-sqlite</c> copies the file ring it kept on the data volume
    /// </remarks>
    private static WebApplicationBuilder AddAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<CatalogueAccessOptions>()
            .BindConfiguration(CatalogueOptions.Section)
            .Validate(options => options.OrganizationId != Guid.Empty,
                "Catalogue:OrganizationId must name the Identity organization this catalogue belongs to.")
            .ValidateOnStart();
        builder.Services.AddIdentityBff();
        builder.Services.AddPostgresDistributedCache();
        builder.Services.AddOptions<PostgresDistributedCacheOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            options.ConnectionString = configuration.GetConnectionString(CatalogueDatabaseConstants.ConnectionStringName) ?? string.Empty;
            options.Schema = CatalogueDatabaseConstants.CacheSchema;
        });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAuthorizationHandler, CatalogueAccessHandler>();
        AuthorizationPolicy Policy(bool approval) => new AuthorizationPolicyBuilder(BffAuthenticationSchemeConstants.Cookie)
            .RequireAuthenticatedUser()
            .AddRequirements(new CatalogueAccessRequirement(approval))
            .Build();
        var member = Policy(approval: false);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(CatalogueAccess.MemberPolicy, member)
            .AddPolicy(CatalogueAccess.ApprovePolicy, Policy(approval: true))
            .SetFallbackPolicy(member);

        // The application name is the one Catalogue has always used, so the copied keys still read what they protected;
        // it resolves from the final configuration, after test and deployment overrides.
        builder.Services.AddDataProtection();
        builder.Services.AddOptions<KeyManagementOptions>().Configure<NpgsqlDataSource>((options, dataSource) =>
            options.XmlRepository = new PostgresXmlRepository(dataSource));
        builder.Services.AddOptions<DataProtectionOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            options.ApplicationDiscriminator = configuration["DataProtection:ApplicationName"] ?? $"ocharo-catalogue:{environment.EnvironmentName}");
        return builder;
    }

    /// <summary>
    /// Configures sign-in in the pipeline, after routing: HTTPS for the session routes, then the BFF.
    /// </summary>
    private static IApplicationBuilder UseAccess(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            // Sign-in needs the secure cookie; a plaintext request would end in a session the browser drops.
            if (context.Request.Path.StartsWithSegments("/api/identity") && !context.Request.IsHttps)
            {
                await context.WriteProblemAsync(CatalogueHttpErrors.HttpsRequired());
                return;
            }
            await next(context);
        });
        app.UseIdentityBff();
        return app;
    }

    /// <summary>Maps the Identity session routes: <c>GET /api/identity/session</c>, <c>GET /api/identity/sign-in</c>,
    /// <c>POST /api/identity/sign-out</c> and the code callback.</summary>
    private static WebApplication MapAccess(this WebApplication app)
    {
        app.MapIdentityBffEndpoints();
        return app;
    }
}
