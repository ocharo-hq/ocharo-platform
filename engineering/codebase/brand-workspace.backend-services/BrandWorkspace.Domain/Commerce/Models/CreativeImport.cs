namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents a recorded creative import and its status.</summary>
public sealed record CreativeImport(Guid Id, DateTimeOffset CreatedAt, string Status, CreativeImportInput Input);
