using System.Text.Json;
using System.Text.Json.Serialization;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Application.Preparation.Models;

namespace BrandWorkspace.Infrastructure.Events.Services;

/// <summary>Adds events from an explicit bundle without overwriting anything already in Desk.</summary>
public sealed class EventImportService(IEventRepository repository, EventValidationService validation)
{
    public const string Format = "ocharo.events-import.v1";
    private const long MaximumBundleBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions BundleJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        MaxDepth = 16
    };

    public async Task<EventImportReport> ImportAsync(
        OrganizationAccess access, string bundlePath, bool dryRun, CancellationToken token)
    {
        if (!access.CanEdit) throw new PreparationException("forbidden", "An editor or owner role is required.");
        PlannedBundle plan = await ReadAsync(bundlePath, token);
        var report = new EventImportReport(dryRun, plan.Source);
        foreach (PlannedEvent planned in plan.Events)
        {
            try
            {
                // An existing key is never touched, so statuses and notes edited in Desk survive every re-import.
                if (await repository.FindEventByKeyAsync(access.OrganizationId, planned.Key, token) is not null)
                {
                    report.Existing++;
                    continue;
                }
                if (!dryRun)
                    await repository.CreateEventAsync(access.OrganizationId, access.UserId, planned.Key, planned.Input, token);
                report.Created++;
            }
            catch (PreparationException exception)
            {
                report.Failures.Add(new EventImportFailure(planned.Reference, exception.Message));
            }
        }
        return report;
    }

    /// <summary>Validates the whole bundle before any write; any invalid event stops the import.</summary>
    private async Task<PlannedBundle> ReadAsync(string bundlePath, CancellationToken token)
    {
        var file = new FileInfo(bundlePath);
        if (!file.Exists) throw new PreparationException("validation", "The bundle file was not found.");
        if (file.Length > MaximumBundleBytes) throw new PreparationException("too-large", "The bundle exceeds 16 MiB.");
        BundleDocument? document;
        try
        {
            await using FileStream stream = file.OpenRead();
            document = await JsonSerializer.DeserializeAsync<BundleDocument>(stream, BundleJson, token);
        }
        catch (JsonException exception)
        {
            throw new PreparationException("validation", "The bundle is not valid JSON: " + exception.Message);
        }
        if (document is null || document.Format != Format)
            throw new PreparationException("validation", $"The bundle format must be {Format}.");
        if (document.Events is null)
            throw new PreparationException("validation", "The bundle must contain an events array; nothing was imported.");
        var errors = new List<string>();
        var events = new List<PlannedEvent>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < document.Events.Count; index++)
        {
            BundleEvent? item = document.Events[index];
            string reference = $"events[{index}] {item?.Key}".TrimEnd();
            try
            {
                if (item is null) throw new PreparationException("validation", "Each event must be an object.");
                string key = validation.Key(item.Key) ?? throw new PreparationException("validation", "An event key is required.");
                if (!keys.Add(key)) throw new PreparationException("validation", $"Event key '{key}' is repeated.");
                ShowcaseEventInput input = validation.Event(new EventFields(
                    item.Title, item.Organizer, item.Scope, item.Format, item.Country, item.City, item.Venue, item.StartsOn,
                    item.EndsOn, item.DatePrecision, item.ApplicationDeadline, item.Participation, item.Cost, item.Website,
                    item.SourceUrl, item.Status, item.Priority, item.Notes), EventValidationService.ImportedStatus);
                events.Add(new PlannedEvent(reference, key, input));
            }
            catch (PreparationException exception) { errors.Add($"{reference}: {exception.Message}"); }
        }
        if (errors.Count > 0)
            throw new PreparationException("validation", "The bundle is invalid; nothing was imported." + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Take(50)));
        return new PlannedBundle(document.Source, events);
    }

    private sealed record PlannedBundle(string? Source, IReadOnlyList<PlannedEvent> Events);
    private sealed record PlannedEvent(string Reference, string Key, ShowcaseEventInput Input);

    private sealed record BundleDocument(string? Format, string? Source, IReadOnlyList<BundleEvent?>? Events);
    private sealed record BundleEvent(
        string? Key, string? Title, string? Organizer, string? Scope, string? Format, string? Country, string? City, string? Venue,
        string? StartsOn, string? EndsOn, string? DatePrecision, string? ApplicationDeadline, string? Participation, string? Cost,
        string? Website, string? SourceUrl, string? Status, string? Priority, string? Notes);
}

/// <summary>Counts what an import created or found already present, and the events it could not store.</summary>
public sealed class EventImportReport(bool dryRun, string? source)
{
    public bool DryRun { get; } = dryRun;
    public string? Source { get; } = source;
    public int Created { get; set; }
    public int Existing { get; set; }
    public List<EventImportFailure> Failures { get; } = [];
}

/// <summary>One bundle event that was not stored, with the reason.</summary>
public sealed record EventImportFailure(string Item, string Reason);
