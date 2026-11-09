using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Api.Brand.Requests;

/// <summary>Represents the save-identity request body.</summary>
public sealed record SaveIdentityApiRequest
{
    /// <summary>Gets the draft revision the editor opened.</summary>
    public int ExpectedRevision { get; init; }

    /// <summary>Gets the edited identity.</summary>
    public required BrandIdentity Identity { get; init; }
}
