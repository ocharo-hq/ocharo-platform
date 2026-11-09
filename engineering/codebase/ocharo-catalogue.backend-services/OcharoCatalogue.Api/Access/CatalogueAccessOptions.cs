using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace OcharoCatalogue.Api.Access;

/// <summary>Holds the organization this catalogue belongs to (section <c>Catalogue</c>).</summary>
public sealed class CatalogueAccessOptions
{
    /// <summary>Gets or sets the Identity organization whose members use this catalogue; <c>Catalogue:OrganizationId</c>,
    /// required at boot.</summary>
    public Guid OrganizationId { get; set; }
}
