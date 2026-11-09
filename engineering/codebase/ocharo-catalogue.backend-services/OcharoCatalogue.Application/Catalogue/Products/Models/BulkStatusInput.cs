namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents one status to set on several products.</summary>
public sealed record BulkStatusInput(IReadOnlyList<string> ProductIds, string Status);
