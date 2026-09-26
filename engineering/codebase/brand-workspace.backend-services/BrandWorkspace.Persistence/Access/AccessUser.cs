using Microsoft.AspNetCore.Identity;

namespace BrandWorkspace.Persistence.Access;

/// <summary>Operator identity managed by the framework password and lockout engine.</summary>
public sealed class AccessUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
