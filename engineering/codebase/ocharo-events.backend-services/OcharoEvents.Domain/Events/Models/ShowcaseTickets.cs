namespace OcharoEvents.Domain.Events.Models;

/// <summary>What the event's source says attending costs, apart from Ocharo's own cost of taking part; an unknown price stays
/// null, never 0.</summary>
/// <param name="MinPrice">The lowest price.</param>
/// <param name="MaxPrice">The highest price.</param>
/// <param name="Currency">The ISO 4217 currency of the prices.</param>
/// <param name="Free">Whether attending is free.</param>
public sealed record ShowcaseTickets(decimal? MinPrice, decimal? MaxPrice, string? Currency, bool Free);
