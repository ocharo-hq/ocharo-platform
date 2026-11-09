namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents the approval a creative source carried when it was exported.</summary>
public sealed record CreativeSourceApproval(string Status, string? Note);
