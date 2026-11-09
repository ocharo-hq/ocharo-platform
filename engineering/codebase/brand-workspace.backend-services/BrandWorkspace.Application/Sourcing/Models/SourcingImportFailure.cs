namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>One bundle item that was not stored, with the reason.</summary>
public sealed record SourcingImportFailure(string Item, string Reason);
