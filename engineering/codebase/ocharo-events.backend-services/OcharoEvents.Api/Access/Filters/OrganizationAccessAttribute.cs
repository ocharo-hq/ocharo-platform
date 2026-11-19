using Microsoft.AspNetCore.Mvc;

namespace OcharoEvents.Api.Access.Filters;

/// <summary>
/// Runs <see cref="OrganizationAccessFilter"/> before each action of an organization-scoped controller, ahead of the
/// automatic model-state check (order -2000), so another organization's path answers 404 whatever body it carries.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OrganizationAccessAttribute : TypeFilterAttribute
{
    /// <summary>Holds the filter's order: before the automatic model-state check.</summary>
    public const int FilterOrder = -3000;

    /// <summary>Initializes the attribute.</summary>
    public OrganizationAccessAttribute() : base(typeof(OrganizationAccessFilter)) => Order = FilterOrder;
}
