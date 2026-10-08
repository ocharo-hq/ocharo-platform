using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Sourcing.Models;
using BrandWorkspace.Application.Sourcing.Repositories;
using BrandWorkspace.Domain.Sourcing.Models;

namespace BrandWorkspace.Infrastructure.Sourcing.Services;

/// <summary>Adds private supplier records from an explicit bundle without overwriting anything already in Desk.</summary>
public sealed partial class SourcingImportService(
    ISourcingRepository repository,
    SourcingValidationService validation,
    SourcingMediaInspector inspector,
    SourcingService sourcing)
{
    public const string Format = "ocharo.sourcing-import.v1";
    private const long MaximumBundleBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions BundleJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        MaxDepth = 16
    };

    public async Task<SourcingImportReport> ImportAsync(
        OrganizationAccess access, string bundlePath, bool dryRun, CancellationToken token)
    {
        if (!access.CanEdit) throw new PreparationException("forbidden", "An editor or owner role is required.");
        PlannedBundle plan = await ReadAsync(bundlePath, token);
        var report = new SourcingImportReport(dryRun, plan.Source);
        var supplierIds = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (PlannedSupplier planned in plan.Suppliers)
        {
            Supplier? supplier = await repository.FindSupplierByKeyAsync(access.OrganizationId, planned.Key, token);
            if (supplier is null)
            {
                report.Suppliers.Created++;
                if (!dryRun)
                    supplier = await repository.CreateSupplierAsync(access.OrganizationId, access.UserId, planned.Key, planned.Input, token);
            }
            else report.Suppliers.Existing++;
            if (supplier is not null) supplierIds[planned.Key] = supplier.Id;

            var personIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
            foreach (PlannedPerson person in planned.People)
            {
                SupplierPerson? existing = supplier?.People.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, person.Input.Name, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    report.People.Existing++;
                    personIds[person.Key] = existing.Id;
                    continue;
                }
                report.People.Created++;
                if (dryRun || supplier is null) continue;
                SupplierPerson created = await repository.CreatePersonAsync(
                    access.OrganizationId, access.UserId, supplier.Id, person.Input, token);
                personIds[person.Key] = created.Id;
            }

            // An imported route never replaces a primary route chosen in Desk.
            bool hasPrimary = supplier?.Routes.Any(route => route.IsPrimary) ?? false;
            foreach (PlannedRoute route in planned.Routes)
            {
                if (supplier?.Routes.Any(existing => existing.Channel == route.Input.Channel
                        && existing.Destination == route.Input.Destination) == true)
                {
                    report.Routes.Existing++;
                    continue;
                }
                report.Routes.Created++;
                if (dryRun || supplier is null) continue;
                RouteInput input = route.Input with
                {
                    IsPrimary = route.Input.IsPrimary && !hasPrimary,
                    PersonId = route.PersonKey is not null && personIds.TryGetValue(route.PersonKey, out Guid personId) ? personId : null
                };
                await repository.CreateRouteAsync(access.OrganizationId, access.UserId, supplier.Id, input, token);
                hasPrimary |= input.IsPrimary;
            }
        }

        foreach (PlannedMedia media in plan.Media)
        {
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(media.Path, token);
                InspectedMedia content = await inspector.InspectAsync(bytes, token);
                if (media.Sha256 is not null && media.Sha256 != content.Sha256)
                    throw new PreparationException("validation", "The file's SHA-256 differs from the bundle.");
                if (await repository.FindMediaBySha256Async(access.OrganizationId, content.Sha256, token) is not null)
                {
                    report.Media.Existing++;
                    continue;
                }
                if (!dryRun)
                {
                    Guid? supplierId = media.SupplierKey is not null && supplierIds.TryGetValue(media.SupplierKey, out Guid id) ? id : null;
                    await sourcing.StoreAsync(access, new NewSourcingMedia(
                        supplierId, content, media.Capture, media.OriginalName, media.Caption, media.SourceLabel,
                        media.SourceMessageId, media.ReceivedAt), token);
                }
                report.Media.Created++;
                report.Media.Bytes += bytes.Length;
            }
            catch (Exception exception) when (exception is PreparationException or IOException or UnauthorizedAccessException)
            {
                report.Media.Failed++;
                report.Failures.Add(new SourcingImportFailure(media.Reference, exception.Message));
            }
        }
        return report;
    }

    /// <summary>Validates the whole bundle before any write; structural errors stop the import.</summary>
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
        string root = file.DirectoryName!;
        var errors = new List<string>();
        var suppliers = new List<PlannedSupplier>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < (document.Suppliers?.Count ?? 0); index++)
        {
            BundleSupplier item = document.Suppliers![index];
            string reference = $"suppliers[{index}]";
            try
            {
                string key = validation.Key(item.Key) ?? throw new PreparationException("validation", "A supplier key is required.");
                if (!keys.Add(key)) throw new PreparationException("validation", $"Supplier key '{key}' is repeated.");
                SupplierInput input = validation.Supplier(item.Name, item.Stage, item.Country, item.City, item.Website, item.Notes);
                if (input.Stage == "qualified")
                    throw new PreparationException("validation", "Imports never mark a supplier qualified; review it in Desk.");
                var people = new List<PlannedPerson>();
                var personKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (BundlePerson person in item.People ?? [])
                {
                    string personKey = validation.Key(person.Key) ?? throw new PreparationException("validation", "A person key is required.");
                    if (!personKeys.Add(personKey)) throw new PreparationException("validation", $"Person key '{personKey}' is repeated.");
                    people.Add(new PlannedPerson(personKey, validation.Person(person.Name, person.Role, person.Notes)));
                }
                var routes = new List<PlannedRoute>();
                foreach (BundleRoute route in item.Routes ?? [])
                {
                    if (route.PersonKey is not null && !personKeys.Contains(route.PersonKey))
                        throw new PreparationException("validation", $"Route person '{route.PersonKey}' is not a person of this supplier.");
                    RouteInput routeInput = validation.Route(route.Channel, route.Destination, route.Label, route.SourceUrl,
                        route.Verified, route.IsPrimary, null, route.Notes);
                    if (routes.Any(existing => existing.Input.Channel == routeInput.Channel && existing.Input.Destination == routeInput.Destination))
                        throw new PreparationException("validation", $"Route {routeInput.Channel} {routeInput.Destination} is repeated.");
                    routes.Add(new PlannedRoute(routeInput, route.PersonKey));
                }
                if (routes.Count(route => route.Input.IsPrimary) > 1)
                    throw new PreparationException("validation", "A supplier has at most one primary route.");
                suppliers.Add(new PlannedSupplier(key, input, people, routes));
            }
            catch (PreparationException exception) { errors.Add($"{reference}: {exception.Message}"); }
        }
        var media = new List<PlannedMedia>();
        for (int index = 0; index < (document.Media?.Count ?? 0); index++)
        {
            BundleMedia item = document.Media![index];
            string reference = $"media[{index}] {item.OriginalName ?? item.Path}";
            try
            {
                if (item.SupplierKey is not null && !keys.Contains(item.SupplierKey))
                    throw new PreparationException("validation", $"Supplier '{item.SupplierKey}' is not in the bundle.");
                if (string.IsNullOrWhiteSpace(item.Path)) throw new PreparationException("validation", "A media path is required.");
                if (item.Sha256 is not null && !Sha256Pattern().IsMatch(item.Sha256))
                    throw new PreparationException("validation", "SHA-256 must be 64 lowercase hexadecimal characters.");
                media.Add(new PlannedMedia(
                    reference,
                    item.SupplierKey,
                    Path.GetFullPath(item.Path, root),
                    item.Sha256,
                    validation.Capture(item.Capture),
                    validation.OriginalName(item.OriginalName ?? Path.GetFileName(item.Path)),
                    validation.Caption(item.Caption),
                    validation.SourceLabel(item.SourceLabel),
                    validation.SourceMessageId(item.SourceMessageId),
                    item.ReceivedAt?.ToUniversalTime()));
            }
            catch (PreparationException exception) { errors.Add($"{reference}: {exception.Message}"); }
        }
        if (errors.Count > 0)
            throw new PreparationException("validation", "The bundle is invalid; nothing was imported." + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Take(50)));
        return new PlannedBundle(document.Source, suppliers, media);
    }

    [GeneratedRegex("^[a-f0-9]{64}$")]
    private static partial Regex Sha256Pattern();

    private sealed record PlannedBundle(string? Source, IReadOnlyList<PlannedSupplier> Suppliers, IReadOnlyList<PlannedMedia> Media);
    private sealed record PlannedSupplier(string Key, SupplierInput Input, IReadOnlyList<PlannedPerson> People, IReadOnlyList<PlannedRoute> Routes);
    private sealed record PlannedPerson(string Key, PersonInput Input);
    private sealed record PlannedRoute(RouteInput Input, string? PersonKey);
    private sealed record PlannedMedia(
        string Reference, string? SupplierKey, string Path, string? Sha256, string Capture, string OriginalName, string Caption,
        string? SourceLabel, string? SourceMessageId, DateTimeOffset? ReceivedAt);

    private sealed record BundleDocument(string? Format, string? Source, IReadOnlyList<BundleSupplier>? Suppliers, IReadOnlyList<BundleMedia>? Media);
    private sealed record BundleSupplier(
        string? Key, string? Name, string? Stage, string? Country, string? City, string? Website, string? Notes,
        IReadOnlyList<BundlePerson>? People, IReadOnlyList<BundleRoute>? Routes);
    private sealed record BundlePerson(string? Key, string? Name, string? Role, string? Notes);
    private sealed record BundleRoute(
        string? Channel, string? Destination, string? Label, string? SourceUrl, bool Verified, bool IsPrimary, string? PersonKey, string? Notes);
    private sealed record BundleMedia(
        string? SupplierKey, string? Path, string? Sha256, string? Capture, string? OriginalName, string? Caption,
        string? SourceLabel, string? SourceMessageId, DateTimeOffset? ReceivedAt);
}

/// <summary>Counts what an import created, found already present, or could not store.</summary>
public sealed class SourcingImportReport(bool dryRun, string? source)
{
    public bool DryRun { get; } = dryRun;
    public string? Source { get; } = source;
    public SourcingImportCounts Suppliers { get; } = new();
    public SourcingImportCounts People { get; } = new();
    public SourcingImportCounts Routes { get; } = new();
    public SourcingImportCounts Media { get; } = new();
    public List<SourcingImportFailure> Failures { get; } = [];
}

/// <summary>Created, existing and failed totals for one record kind.</summary>
public sealed class SourcingImportCounts
{
    public int Created { get; set; }
    public int Existing { get; set; }
    public int Failed { get; set; }
    public long Bytes { get; set; }
}

/// <summary>One bundle item that was not stored, with the reason.</summary>
public sealed record SourcingImportFailure(string Item, string Reason);
