using BrandWorkspace.Api.Configurations;

// Build and configure the host.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args.Contains("--migrate") || args.Contains("--provision-operator") || args.Contains("--import-sourcing") || args.Contains("--import-events") || args.Contains("--verify-media-runtime") ? [] : args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configure(args.Contains("--migrate"));

// Build and configure the app.
var app = builder.Build();
app.Configure(serveHttp: !args.Any(argument => argument is "--migrate" or "--provision-operator" or "--import-sourcing" or "--import-events" or "--verify-media-runtime"));

// Run.
await app.RunCommandOrHostAsync(args);

public partial class Program;
