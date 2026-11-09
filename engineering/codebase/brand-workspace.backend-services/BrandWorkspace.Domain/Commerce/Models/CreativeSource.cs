namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents the Studio look and revision a creative derivative came from.</summary>
public sealed record CreativeSource(string Application, string WorkspaceId, string LookId, int Revision);
