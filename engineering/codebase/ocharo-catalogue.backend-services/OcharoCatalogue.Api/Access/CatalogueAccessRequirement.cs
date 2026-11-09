using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace OcharoCatalogue.Api.Access;

/// <summary>Requires <see cref="CatalogueAccess.IsAllowed"/> for the request; <see cref="RequiresApproval"/> marks the
/// publication actions.</summary>
public sealed class CatalogueAccessRequirement(bool requiresApproval) : IAuthorizationRequirement
{
    /// <summary>Gets whether the endpoint also needs <see cref="CatalogueAccess.ApprovePermission"/>.</summary>
    public bool RequiresApproval { get; } = requiresApproval;
}
