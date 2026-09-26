namespace BrandWorkspace.Api.Access;

/// <summary>Marks the unknown-API fallback, which always returns 404 and cannot mutate state.</summary>
public sealed class SkipAntiforgeryValidation;
