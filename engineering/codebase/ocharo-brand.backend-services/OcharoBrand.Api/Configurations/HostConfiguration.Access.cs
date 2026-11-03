using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OcharoBrand.Api.Access;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Identity.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;

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
        // Resolve final configuration after test/deployment overrides have been applied.
        builder.Services.AddDataProtection();
        builder.Services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
        {
            var path = configuration["DataProtection:KeysPath"];
            if (string.IsNullOrWhiteSpace(path))
            {
                if (!environment.IsLocalRun(configuration)) throw new InvalidOperationException("DataProtection:KeysPath must name durable storage.");
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ocharo-brand", environment.EnvironmentName, "keys");
            }
            options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path), NullLoggerFactory.Instance);
        });
        builder.Services.AddOptions<DataProtectionOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            options.ApplicationDiscriminator = configuration["DataProtection:ApplicationName"] ?? $"ocharo-brand:{environment.EnvironmentName}");
        return builder;
    }

    /// <summary>Configures the session pipeline after routing: HTTPS for the session routes, the BFF (authentication, the
    /// <c>X-Csrf-Token</c> check, authorization), then the bound-organization membership and permission check.</summary>
    private static WebApplication UseAccess(this WebApplication app)
    {
        // A deployed host without a durable key path fails here, at boot, rather than at the first sign-in.
        _ = app.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/identity") && !context.Request.IsHttps)
            {
                await Results.Problem(statusCode: 400, title: "HTTPS is required",
                    extensions: new Dictionary<string, object?> { ["code"] = "https_required" }).ExecuteAsync(context);
                return;
            }
            await next(context);
        });
        app.UseIdentityBff();
        app.UseMiddleware<BrandAccessMiddleware>();
        return app;
    }
}
