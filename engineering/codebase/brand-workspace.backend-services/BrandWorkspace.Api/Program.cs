using BrandWorkspace.Api.Configurations;

// Build and configure the host.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args.Where(argument => argument != "--migrate").ToArray(),
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configure(args.Contains("--migrate"));

// Build and configure the app.
var app = builder.Build();
app.Configure();

// Run.
await app.RunCommandOrHostAsync(args);

public partial class Program;
