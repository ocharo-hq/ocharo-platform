namespace OcharoBrand.Api.Brand.Requests;

/// <summary>Represents the set-logo-status request body.</summary>
public sealed record SetLogoStatusApiRequest
{
    /// <summary>Gets the status the version takes: <c>draft</c> or <c>approved</c>.</summary>
    public required string Status { get; init; }
}
