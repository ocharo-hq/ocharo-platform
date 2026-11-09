namespace OcharoBrand.Api.Brand.Requests;

/// <summary>Represents the publish-release request body.</summary>
public sealed record PublishReleaseApiRequest
{
    /// <summary>Gets the draft revision the approver reviewed.</summary>
    public int ExpectedRevision { get; init; }

    /// <summary>Gets the release note.</summary>
    public string? Note { get; init; }
}
