using Microsoft.AspNetCore.Antiforgery;

namespace BrandWorkspace.Api.Access;

/// <summary>Invalidates request tokens when the authenticated session changes.</summary>
public sealed class SessionAntiforgeryDataProvider : IAntiforgeryAdditionalDataProvider
{
    public string GetAdditionalData(HttpContext context) => AccessClaims.Session(context.User)?.ToString("D") ?? "anonymous";
    public bool ValidateAdditionalData(HttpContext context, string additionalData) =>
        string.Equals(GetAdditionalData(context), additionalData, StringComparison.Ordinal);
}
