namespace BrandWorkspace.Api.Foundation.Models;

/// <summary>Represents the record a delete removed.</summary>
/// <param name="Id">The deleted record's id.</param>
public sealed record RecordDeletedDto(Guid Id);
