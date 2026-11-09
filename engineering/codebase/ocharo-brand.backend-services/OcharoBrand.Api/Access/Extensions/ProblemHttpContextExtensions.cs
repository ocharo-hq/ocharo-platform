using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Factories;

namespace OcharoBrand.Api.Access.Extensions;

/// <summary>Answers a request from middleware with the SDK problem details of an application error.</summary>
public static class ProblemHttpContextExtensions
{
    /// <summary>Writes <paramref name="error"/> through the registered problem factory and writer, as a controller's failure
    /// arm and the exception handlers do.</summary>
    public static Task WriteProblemAsync(this HttpContext http, AppError error)
    {
        var services = http.RequestServices;
        var problem = services.GetRequiredService<IAppErrorProblemDetailsFactory>().Create(error, http);
        return services.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problem }).AsTask();
    }
}
