using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Web.ErrorMapping;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>A catalogue request the mediator refused, with the HTTP status the API answers it with.</summary>
public sealed class CatalogueRejection(AppError error) : Exception(error.Message)
{
    /// <summary>The refusal.</summary>
    public AppError Error { get; } = error;

    /// <summary>The status the API maps the refusal to.</summary>
    public int StatusCode { get; } = new ErrorHttpStatusCodeMapper().ToStatusCode(error);
}
