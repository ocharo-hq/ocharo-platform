using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using BrandWorkspace.Application.Foundation.Errors;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Repositories;
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
        if (!access.CanEdit) throw DeskErrors.Forbidden("An editor or owner role is required.").ToException();
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
                    throw DeskErrors.Invalid("The file's SHA-256 differs from the bundle.").ToException();
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
            catch (Exception exception) when (exception is AppException or IOException or UnauthorizedAccessException)
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
        if (!file.Exists) throw DeskErrors.Invalid("The bundle file was not found.").ToException();
        if (file.Length > MaximumBundleBytes) throw DeskErrors.TooLarge("The bundle exceeds 16 MiB.").ToException();
        BundleDocument? document;
        try
        {
            await using FileStream stream = file.OpenRead();
            document = await JsonSerializer.DeserializeAsync<BundleDocument>(stream, BundleJson, token);
        }
        catch (JsonException exception)
        {
            throw DeskErrors.Invalid("The bundle is not valid JSON: " + exception.Message).ToException();
        }
        if (document is null || document.Format != Format)
            throw DeskErrors.Invalid($"The bundle format must be {Format}.").ToException();
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
                string key = validation.Key(item.Key) ?? throw DeskErrors.Invalid("A supplier key is required.").ToException();
                if (!keys.Add(key)) throw DeskErrors.Invalid($"Supplier key '{key}' is repeated.").ToException();
                SupplierInput input = validation.Supplier(item.Name, item.Stage, item.Country, item.City, item.Website, item.Notes);
                if (input.Stage == "qualified")
                    throw DeskErrors.Invalid("Imports never mark a supplier qualified; review it in Desk.").ToException();
                var people = new List<PlannedPerson>();
                var personKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (BundlePerson person in item.People ?? [])
                {
                    string personKey = validation.Key(person.Key) ?? throw DeskErrors.Invalid("A person key is required.").ToException();
                    if (!personKeys.Add(personKey)) throw DeskErrors.Invalid($"Person key '{personKey}' is repeated.").ToException();
                    people.Add(new PlannedPerson(personKey, validation.Person(person.Name, person.Role, person.Notes)));
                }
                var routes = new List<PlannedRoute>();
                foreach (BundleRoute route in item.Routes ?? [])
                {
                    if (route.PersonKey is not null && !personKeys.Contains(route.PersonKey))
                        throw DeskErrors.Invalid($"Route person '{route.PersonKey}' is not a person of this supplier.").ToException();
                    RouteInput routeInput = validation.Route(route.Channel, route.Destination, route.Label, route.SourceUrl,
                        route.Verified, route.IsPrimary, null, route.Notes);
                    if (routes.Any(existing => existing.Input.Channel == routeInput.Channel && existing.Input.Destination == routeInput.Destination))
                        throw DeskErrors.Invalid($"Route {routeInput.Channel} {routeInput.Destination} is repeated.").ToException();
                    routes.Add(new PlannedRoute(routeInput, route.PersonKey));
                }
                if (routes.Count(route => route.Input.IsPrimary) > 1)
                    throw DeskErrors.Invalid("A supplier has at most one primary route.").ToException();
                suppliers.Add(new PlannedSupplier(key, input, people, routes));
            }
            catch (AppException exception) { errors.Add($"{reference}: {exception.Message}"); }
        }
        var media = new List<PlannedMedia>();
        for (int index = 0; index < (document.Media?.Count ?? 0); index++)
        {
            BundleMedia item = document.Media![index];
            string reference = $"media[{index}] {item.OriginalName ?? item.Path}";
            try
            {
                if (item.SupplierKey is not null && !keys.Contains(item.SupplierKey))
                    throw DeskErrors.Invalid($"Supplier '{item.SupplierKey}' is not in the bundle.").ToException();
                if (string.IsNullOrWhiteSpace(item.Path)) throw DeskErrors.Invalid("A media path is required.").ToException();
                if (item.Sha256 is not null && !Sha256Pattern().IsMatch(item.Sha256))
                    throw DeskErrors.Invalid("SHA-256 must be 64 lowercase hexadecimal characters.").ToException();
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
            catch (AppException exception) { errors.Add($"{reference}: {exception.Message}"); }
        }
        if (errors.Count > 0)
            throw DeskErrors.Invalid("The bundle is invalid; nothing was imported." + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Take(50))).ToException();
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
