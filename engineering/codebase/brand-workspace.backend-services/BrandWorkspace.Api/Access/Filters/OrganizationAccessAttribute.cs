using Microsoft.AspNetCore.Mvc;

namespace BrandWorkspace.Api.Access.Filters;

/// <summary>Runs <see cref="OrganizationAccessFilter"/> before each action of an organization-scoped controller.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OrganizationAccessAttribute() : TypeFilterAttribute(typeof(OrganizationAccessFilter));
