namespace OcharoBrand.Domain.Brand;

/// <summary>An immutable published brand kit. Apps can follow the latest or pin a version.</summary>
public sealed record BrandRelease(int Version, DateTimeOffset PublishedAt, string Note, BrandIdentity Identity, IReadOnlyDictionary<string, LogoVersion> Logos);
