namespace OcharoBrand.Api.Brand.Requests;

/// <summary>Represents the set-current-logo request body.</summary>
public sealed record SetCurrentLogoApiRequest
{
    /// <summary>Gets the draft revision the editor opened.</summary>
    public int ExpectedRevision { get; init; }

    /// <summary>Gets the logo version the slot takes.</summary>
    public required string VersionId { get; init; }
}
