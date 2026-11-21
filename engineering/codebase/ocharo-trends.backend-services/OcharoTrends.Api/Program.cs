using OcharoTrends.Api.Access;
using OcharoTrends.Api.Trends;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Meta;

return await ServiceHost.RunAsync(args, builder =>
{
    builder.AddApiDefaults(options =>
    {
        options.ServiceName = "ocharo-trends";
        options.EnableOtlpExporters = false;
        options.EnableRateLimiting = false;
        options.EnableOutputCache = false;
        options.EnableCrossOriginEmbedderPolicy = false;
        options.EnableHttpsRedirection = false;
        options.TrustProxiesFrom(builder.Configuration.GetSection("Deployment"));
        options.SpaHosting = _ => { };
    });
    builder.Services.AddSingleton(provider => new TrendLibrary(
        provider.GetRequiredService<IConfiguration>(), provider.GetRequiredService<IHostEnvironment>()));
    builder.AddReviewAccess();
}, async app =>
{
    _ = app.Services.GetRequiredService<TrendLibrary>();
    var access = app.Services.GetRequiredService<ReviewAccess>();
    if (access.Enabled)
        await app.Services.MigrateBespokeOnStartupAsync(app.Lifetime.ApplicationStopping);

    app.UseApiDefaults(pipeline => pipeline.UseReviewAccess(access));
    app.MapTrends(access);
});

public partial class Program;
