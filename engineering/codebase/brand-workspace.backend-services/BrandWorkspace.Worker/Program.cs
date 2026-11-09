using System.Reflection;
using BrandWorkspace.Worker.Configurations;
using Microsoft.Extensions.Hosting;

// Build and configure the host.
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev";
Console.Error.WriteLine($"BrandWorkspace worker {version}");

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = [],
    ContentRootPath = AppContext.BaseDirectory
});
builder.Configure(args.Length == 0);

// Build and configure the app.
using var host = builder.Build();
await host.ConfigureAsync();

// Run.
Environment.ExitCode = await host.RunCommandOrHostAsync(args);
