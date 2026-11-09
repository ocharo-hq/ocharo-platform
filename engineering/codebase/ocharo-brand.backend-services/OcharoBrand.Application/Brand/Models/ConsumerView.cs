namespace OcharoBrand.Application.Brand.Models;

/// <summary>Represents an app that reads the public brand, and whether it last saw the latest release.</summary>
public sealed record ConsumerView(string App, DateTimeOffset LastSeenAt, int LastRelease, bool Current, string LastPath);
