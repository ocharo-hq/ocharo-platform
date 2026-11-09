using Microsoft.Extensions.Options;
using OcharoCatalogue.Api.Access;
using OcharoCatalogue.Application.Catalogue.Core.Services;

namespace OcharoCatalogue.Api.Catalogue.Core.Services;

/// <summary>
/// Provides the request's caller facts from its Identity session and the organization this catalogue belongs to.
/// </summary>
public sealed class HttpCatalogueCaller(
    IHttpContextAccessor accessor,
    IOptions<CatalogueAccessOptions> access) : ICatalogueCaller
{
    /// <inheritdoc />
    public bool IsMember
    {
        get
        {
            return accessor.HttpContext?.User is { } user && CatalogueAccess.IsMember(
                user,
                access.Value.OrganizationId);
        }
    }
}
