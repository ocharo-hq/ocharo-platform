namespace OcharoBrand.Application.Brand.Models;

/// <summary>Represents one saved draft revision.</summary>
public sealed record RevisionSummary(int Revision, DateTimeOffset SavedAt, string Summary);
