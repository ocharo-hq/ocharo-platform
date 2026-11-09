namespace BrandWorkspace.Application.Events.Models;

/// <summary>One bundle event that was not stored, with the reason.</summary>
public sealed record EventImportFailure(string Item, string Reason);
