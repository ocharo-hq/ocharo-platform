using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Factories;

namespace OcharoCatalogue.Api.Catalogue.Core.Extensions;

/// <summary>
/// Extends the request with the SDK's problem details for an error refused before any controller runs.
/// </summary>
public static class HttpContextProblemExtensions
{
    /// <summary>
    /// Writes <paramref name="error"/> as the request's problem details, through the shared factory and writer.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="error">The error to answer with.</param>
    public static async Task WriteProblemAsync(this HttpContext context, AppError error)
    {
        var factory = context.RequestServices.GetRequiredService<IAppErrorProblemDetailsFactory>();
        var problems = context.RequestServices.GetRequiredService<IProblemDetailsService>();
        var problem = factory.Create(error, context);
        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
