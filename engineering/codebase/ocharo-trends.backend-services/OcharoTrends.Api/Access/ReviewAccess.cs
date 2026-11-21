using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Npgsql;
using OcharoTrends.Api.Persistence;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Identity.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace OcharoTrends.Api.Access;

internal sealed record ReviewAccess(Guid OrganizationId, bool Enabled)
{
    internal const string MemberPolicy = "trends-member";
    internal const string EditorPolicy = "trends-editor";
    internal const string EditPermission = "trends:edit";

    internal static string? Subject(ClaimsPrincipal principal) => principal.FindFirstValue("sub")
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
}

internal static class ReviewAccessConfiguration
{
    internal static void AddReviewAccess(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var database = configuration.GetConnectionString("Database");
        var identityConfigured = configuration.GetSection("Identity:Client").Exists();
        var organizationConfigured = configuration["Trends:OrganizationId"] is not null;
        var organizationValid = Guid.TryParse(configuration["Trends:OrganizationId"], out var organization)
            && organization != Guid.Empty;

        if (builder.Environment.IsDevelopment() && !identityConfigured && !organizationConfigured
            && string.IsNullOrWhiteSpace(database))
        {
            builder.Services.AddSingleton(new ReviewAccess(Guid.Empty, false));
            return;
        }

        if (string.IsNullOrWhiteSpace(database) || !identityConfigured || !organizationValid)
            throw new InvalidOperationException("Configure ConnectionStrings:Database, Identity:Client and Trends:OrganizationId together to enable review.");

        builder.Services.AddSingleton(new ReviewAccess(organization, true));
        builder.Services.AddIdentityBff(options => options.CookieName = "__Host-ocharo-trends");
        builder.Services.AddDefaultDenyAuthorization(BffAuthenticationSchemeConstants.Cookie);
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(ReviewAccess.MemberPolicy, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                Guid.TryParse(context.User.FindFirstValue(OrganizationClaimTypeConstants.Id), out var activeOrganization)
                && activeOrganization == organization
                && context.User.GetOrganizationRole(organization) is not null))
            .AddPolicy(ReviewAccess.EditorPolicy, policy => policy.RequireAuthenticatedUser()
                .RequirePermission(ReviewAccess.EditPermission)
                .RequireAssertion(context => !string.IsNullOrWhiteSpace(ReviewAccess.Subject(context.User))));

        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(database));
        builder.Services.AddSingleton<ReviewStore>();
        builder.Services.AddKeyedSingleton<IDbConnectionFactory>("trends", static (provider, _) =>
            new DataSourceConnectionFactory(provider.GetRequiredService<NpgsqlDataSource>()));
        builder.Services.AddKeyedSingleton("trends", (_, _) => new DatabaseSettings { ConnectionString = database });
        builder.Services.AddDatabaseBespokeMigrationSet("trends", static _ =>
            new EmbeddedResourceMigrationBroker(typeof(ReviewStore).Assembly, "OcharoTrends/Migrations/"), migration =>
            {
                migration.SchemaName = "trends";
                migration.SchemaOwner = "product";
                migration.AdvisoryLockId = 8_440_001;
                migration.Version = "v0.0.0";
                migration.Release = "0.0.0";
            });
        builder.Services.AddPostgresDistributedCache(options =>
        {
            options.Schema = "trends_cache";
            options.ConnectionString = database;
        });
        builder.Services.AddDataProtection().SetApplicationName($"ocharo-trends:{builder.Environment.EnvironmentName}");
        builder.Services.AddOptions<KeyManagementOptions>().Configure<NpgsqlDataSource>((options, dataSource) =>
            options.XmlRepository = new TrendsKeyRepository(dataSource));
    }

    internal static void UseReviewAccess(this IApplicationBuilder app, ReviewAccess access)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/identity") || context.Request.Path.StartsWithSegments("/api/trends/review"))
                context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        if (access.Enabled)
            app.UseIdentityBff();
    }
}
