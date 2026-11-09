namespace OcharoBrand.Application.Brand.Models;

/// <summary>Represents one published release and what it changed.</summary>
public sealed record ReleaseSummary(int Version, DateTimeOffset PublishedAt, string Note, IReadOnlyList<string> Changes);
