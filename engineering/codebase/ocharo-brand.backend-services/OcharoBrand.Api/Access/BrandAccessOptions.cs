namespace OcharoBrand.Api.Access;

/// <summary>The organization Brand serves (section <c>Brand</c>): only its members reach the private API.</summary>
public sealed class BrandAccessOptions
{
    public const string Section = "Brand";

    /// <summary>The Ocharo organization's id at the Identity instance (<c>Brand:OrganizationId</c>). Required.</summary>
    public Guid OrganizationId { get; set; }
}
