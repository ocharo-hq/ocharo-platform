using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Api.Access;
using OcharoCatalogue.Api.Catalogue.Core.Errors;
using OcharoCatalogue.Api.Catalogue.Core.Extensions;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Options;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Web.DataProtection;

namespace OcharoCatalogue.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>
    /// Registers sign-in through Ocharo's Identity instance (BFF, section <c>Identity:Client</c>), the bound
    /// organization
    /// (<c>Catalogue:OrganizationId</c>, required at boot), default deny with <see
    /// cref="CatalogueAccess.MemberPolicy"/>
    /// as the fallback, and a Data Protection key ring that survives restarts.
    /// </summary>
    /// <remarks>
    ///   - the BFF keeps its sessions in the SDK's default in-memory <c>IDistributedCache</c>: Catalogue's store is
    /// SQLite
    ///     and the SDK ships no SQLite cache, so a restart or deploy signs everyone in again; the key ring persists, so
    ///     the cookies and tokens it protects stay readable
    ///   - the key ring lives at <c>DataProtection:KeysPath</c>, else <c>{Catalogue:DataRoot}/keys</c>, which a
    /// deployed
    ///     host keeps on its data volume
    /// </remarks>
    private static WebApplicationBuilder AddAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<CatalogueAccessOptions>()
            .BindConfiguration(CatalogueOptions.Section)
            .Validate(options => options.OrganizationId != Guid.Empty,
                "Catalogue:OrganizationId must name the Identity organization this catalogue belongs to.")
            .ValidateOnStart();
        builder.Services.AddIdentityBff();
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

        // The application name and key directory are the ones Catalogue has always used, so existing cookies stay readable;
        // both resolve from the final configuration, after test and deployment overrides.
        builder.Services.AddPersistentDataProtection(_ => { });
        builder.Services.AddOptions<PersistentDataProtectionOptions>()
            .Configure<IConfiguration, IHostEnvironment, IOptions<CatalogueOptions>>((options, configuration, environment, catalogue) =>
            {
                options.ApplicationName = configuration["DataProtection:ApplicationName"] ?? $"ocharo-catalogue:{environment.EnvironmentName}";
                var path = configuration["DataProtection:KeysPath"];
                if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(catalogue.Value.DataRoot, "keys");
                options.KeyDirectory = Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path);
            });
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
