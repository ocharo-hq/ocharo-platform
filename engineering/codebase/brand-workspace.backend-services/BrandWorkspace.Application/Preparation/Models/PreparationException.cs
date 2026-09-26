namespace BrandWorkspace.Application.Preparation.Models;

/// <summary>A bounded application failure safe to expose as a ProblemDetails response.</summary>
public sealed class PreparationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
