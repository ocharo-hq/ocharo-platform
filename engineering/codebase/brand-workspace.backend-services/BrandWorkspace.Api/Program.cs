using BrandWorkspace.Api.Configurations;

// Build and configure the host.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args.Contains("--migrate") || args.Contains("--provision-operator") || args.Contains("--verify-media-runtime") ? [] : args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configure(args.Contains("--migrate"));

// Build and configure the app.
var app = builder.Build();
app.Configure(serveHttp: !args.Any(argument => argument is "--migrate" or "--provision-operator" or "--verify-media-runtime"));

// Run.
await app.RunCommandOrHostAsync(args);

public partial class Program;
