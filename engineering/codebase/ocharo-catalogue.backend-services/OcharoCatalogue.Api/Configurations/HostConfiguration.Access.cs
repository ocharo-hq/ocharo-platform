using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Api.Access;
using OcharoCatalogue.Infrastructure.Catalogue;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;

namespace OcharoCatalogue.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>
    /// Registers sign-in through Ocharo's Identity instance (BFF, section <c>Identity:Client</c>), the bound organization
    /// (<c>Catalogue:OrganizationId</c>, required at boot), default deny with <see cref="CatalogueAccess.MemberPolicy"/>
    /// as the fallback, and a Data Protection key ring that survives restarts.
    /// </summary>
    /// <remarks>
    ///   - the BFF keeps its sessions in the SDK's default in-memory <c>IDistributedCache</c>: Catalogue's store is SQLite
    ///     and the SDK ships no SQLite cache, so a restart or deploy signs everyone in again; the key ring persists, so
    ///     the cookies and tokens it protects stay readable
    ///   - the key ring lives at <c>DataProtection:KeysPath</c>, else <c>{Catalogue:DataRoot}/keys</c>, which a deployed
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

        // Resolve final configuration after test/deployment overrides have been applied.
        builder.Services.AddDataProtection();
        builder.Services.AddOptions<KeyManagementOptions>()
            .Configure<IConfiguration, IHostEnvironment, IOptions<CatalogueOptions>>((options, configuration, environment, catalogue) =>
            {
                var path = configuration["DataProtection:KeysPath"];
                if (string.IsNullOrWhiteSpace(path)) path = Path.Combine(catalogue.Value.DataRoot, "keys");
                if (!Path.IsPathRooted(path)) path = Path.Combine(environment.ContentRootPath, path);
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path), NullLoggerFactory.Instance);
            });
        builder.Services.AddOptions<DataProtectionOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            options.ApplicationDiscriminator = configuration["DataProtection:ApplicationName"] ?? $"ocharo-catalogue:{environment.EnvironmentName}");
        return builder;
    }

    /// <summary>Adds sign-in to the pipeline and maps the Identity session routes: <c>GET /api/identity/session</c>,
    /// <c>GET /api/identity/sign-in</c>, <c>POST /api/identity/sign-out</c> and the code callback.</summary>
    private static WebApplication UseAccess(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            // Sign-in needs the secure cookie; a plaintext request would end in a session the browser drops.
            if (context.Request.Path.StartsWithSegments("/api/identity") && !context.Request.IsHttps)
            {
                await Results.Problem(statusCode: 400, title: "HTTPS is required",
                    extensions: new Dictionary<string, object?> { ["code"] = "https_required" }).ExecuteAsync(context);
                return;
            }
            await next(context);
        });
        app.UseIdentityBff();
        app.MapIdentityBffEndpoints();
        return app;
    }
}
