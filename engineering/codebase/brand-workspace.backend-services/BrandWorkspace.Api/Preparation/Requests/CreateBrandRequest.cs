namespace BrandWorkspace.Api.Preparation.Requests;

/// <summary>Creates brand configuration inside the route's authorized organization.</summary>
public sealed record CreateBrandRequest(string Name, string TimeZone);
