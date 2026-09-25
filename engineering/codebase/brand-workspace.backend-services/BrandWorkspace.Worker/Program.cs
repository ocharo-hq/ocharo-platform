using BrandWorkspace.Worker.Configurations;
using Microsoft.Extensions.Hosting;

// Build and configure the host.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = [], ContentRootPath = AppContext.BaseDirectory
});
builder.Configure(args.Length == 0);

// Build and configure the app.
using var host = builder.Build();
host.Configure();

// Run.
Environment.ExitCode = await host.RunCommandOrHostAsync(args);
