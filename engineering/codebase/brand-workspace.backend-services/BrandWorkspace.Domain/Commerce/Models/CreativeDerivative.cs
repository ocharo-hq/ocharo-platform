namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents the image a creative import brings into the brand's media.</summary>
public sealed record CreativeDerivative(Guid MediaId, string Sha256, string MediaType);
