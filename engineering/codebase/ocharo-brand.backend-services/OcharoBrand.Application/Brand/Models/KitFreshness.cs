namespace OcharoBrand.Application.Brand.Models;

/// <summary>Represents how long the public kit stays as it is on its own.</summary>
/// <param name="SecondsToNextBoundary">Seconds until a scheduled appearance starts or ends; none when nothing is scheduled.</param>
public sealed record KitFreshness(int? SecondsToNextBoundary);
