using Microsoft.Extensions.Hosting;

namespace BrandWorkspace.Worker.Configurations;

/// <summary>Extends the host builder and the worker for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the worker builder with either execution or client-only composition.</summary>
    public static HostApplicationBuilder Configure(this HostApplicationBuilder builder, bool executeJobs) =>
        builder.AddExplicitEnvironment().AddPostgresDatabase().AddFoundation(executeJobs);

    /// <summary>Configures the worker without applying database migrations.</summary>
    public static IHost Configure(this IHost host) => host;
}
