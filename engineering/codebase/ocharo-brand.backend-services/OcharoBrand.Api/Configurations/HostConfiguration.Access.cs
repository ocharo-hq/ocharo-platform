using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Options;
using OcharoBrand.Api.Access;
using OcharoBrand.Api.Access.Extensions;
using OcharoBrand.Application.Brand.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Identity.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Web.DataProtection;

namespace OcharoBrand.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers sign-in through Ocharo's Identity instance (BFF, section <c>Identity:Client</c>), default deny, the
    /// bound organization (<c>Brand:OrganizationId</c>) and the Data Protection key ring that encrypts the sessions.</summary>
    /// <remarks>The BFF keeps sessions in the SDK default, the in-memory distributed cache: Brand stores in SQLite and no
    /// SQLite <c>IDistributedCache</c> exists, so a restart or deploy signs everyone out once. The key ring persists to
    /// <c>DataProtection:KeysPath</c>, required on every host but a local run.</remarks>
    private static WebApplicationBuilder AddAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentityBff();
        builder.Services.AddDefaultDenyAuthorization(BffAuthenticationSchemeConstants.Cookie);
        builder.Services.AddOptions<BrandAccessOptions>().BindConfiguration(BrandAccessOptions.Section)
            .Validate(options => options.OrganizationId != Guid.Empty, "Brand:OrganizationId must name the Ocharo organization Brand serves.")
            .ValidateOnStart();
        // Today's application name and key path, unchanged, so existing sessions stay readable. Resolved from the final
        // configuration, after test and deployment overrides; a local run without a path keeps its per-machine folder.
        builder.Services.AddPersistentDataProtection(_ => { });
        builder.Services.AddOptions<PersistentDataProtectionOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
        {
            options.ApplicationName = configuration["DataProtection:ApplicationName"] ?? $"ocharo-brand:{environment.EnvironmentName}";
            options.KeyDirectory = configuration["DataProtection:KeysPath"] is { } path && !string.IsNullOrWhiteSpace(path) ? path
                : environment.IsLocalRun(configuration)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ocharo-brand", environment.EnvironmentName, "keys")
                    : null;
        });
        return builder;
    }

    /// <summary>Configures the session pipeline after routing: HTTPS for the session routes, the BFF (authentication, the
    /// <c>X-Csrf-Token</c> check, authorization), then the bound-organization membership and permission check.</summary>
    private static IApplicationBuilder UseAccess(this IApplicationBuilder app)
    {
        // A deployed host without a durable key path fails here, at boot, rather than at the first sign-in.
        _ = app.ApplicationServices.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
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
