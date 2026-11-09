namespace OcharoBrand.Application.Brand.Models;

/// <summary>Represents the appearance active in the public kit.</summary>
public sealed record PublicAppearance(string Id, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? AccentHex, string? LinkLabel, string? LinkUrl);
