namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>A review question about one field; resolved issues stay as evidence.</summary>
public sealed record ReviewIssue(string Id, string Field, string Status, string Question);
