using BrandWorkspace.Domain.Events.Models;

namespace BrandWorkspace.Application.Events.Models;

/// <summary>A stored event with the person who recorded it, as the export to WoW2 Events reads it.</summary>
/// <param name="Event">The event.</param>
/// <param name="CreatedBy">The Desk user id of the person who created or imported it.</param>
public sealed record ShowcaseEventRecord(ShowcaseEvent Event, string CreatedBy);
