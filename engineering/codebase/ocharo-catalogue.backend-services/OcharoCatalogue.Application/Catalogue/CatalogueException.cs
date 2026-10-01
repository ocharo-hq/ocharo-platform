namespace OcharoCatalogue.Application.Catalogue;

/// <summary>A rejected catalogue command; the API maps it to ProblemDetails with <see cref="StatusCode"/>.</summary>
public sealed class CatalogueException(string message, int statusCode = 400) : Exception(message)
{
    /// <summary>The HTTP status the rejection maps to.</summary>
    public int StatusCode { get; } = statusCode;

    public static CatalogueException NotFound(string what) => new($"{what} was not found.", 404);

    public static CatalogueException Conflict(string detail) => new(detail, 409);
}
