namespace OcharoBrand.Domain.Brand;

/// <summary>An official destination; <see cref="Handle"/> keeps the account's exact spelling.</summary>
public sealed record BrandLink(string Key, string Label, string? Handle, string Url);
