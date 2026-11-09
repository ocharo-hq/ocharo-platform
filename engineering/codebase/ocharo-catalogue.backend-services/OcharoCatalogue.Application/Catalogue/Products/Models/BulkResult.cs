namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents the outcome of a bulk command.</summary>
public sealed record BulkResult(int Changed, IReadOnlyList<string> Skipped);
