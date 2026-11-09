using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Domain.Preparation.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OcharoBrand.Domain.Brand;
using SkiaSharp;
using WoW.Two.Sdk.Beta.Be.Files.Client;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2TokenExchange;
using WoW.Two.Sdk.Beta.Be.Testing;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Desk on WoW2 Files (<c>Files:BaseUrl</c> set, planning/files-adoption.md #9–#15): each of the 4 kinds lands in Files
/// once under its manifest reference, routes redirect to links or keep answering bytes as decided, rows stored before Files
/// read their PostgreSQL bytes until the boot backfill fills their ids, and the commerce attach references the preparation
/// file. Files itself is <see cref="TestFiles"/>; the database is real PostgreSQL. Files off is every other suite.</summary>
public sealed class FilesMediaTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [Fact]
    public async Task PreparationImagesAndExports_LandInFilesOnce_ContentStaysBytes_AndTheZipDownloadRedirects()
    {
        var owner = await fixture.CreateOwnerAsync();
        var files = new TestFiles();
        await using var host = Host(files);
        using var client = await fixture.LoginAsync(host, owner);
        var path = Organization(owner);
        var brand = await BrandAsync(client, path);
        var bytes = Png(SKColors.Teal, 40, 30);

        var media = await UploadMediaAsync(client, path, bytes);
        var mediaId = media.GetProperty("id").GetGuid();
        var image = files.Of("desk").Should().ContainSingle().Subject;
        var mediaReference = $"{owner.OrganizationId:D}/{mediaId:D}";
        image.Sha256.Should().Be(Sha(bytes));
        files.ReferencesOf(image.Id.ToString()).Should().Contain(new FileReference("desk.preparation-media", mediaReference));
        files.KeyOf(image.Id.ToString()).Should().Be($"desk.preparation-media:{mediaReference}");
        (await RowAsync("SELECT bytes IS NULL, file_id, byte_length FROM preparation.media WHERE organization_id=$1 AND id=$2",
            owner.OrganizationId, mediaId)).Should().Equal(true, image.Id.ToString(), (long)bytes.Length);
        (await Data(await client.GetAsync($"{path}/media")))[0].GetProperty("byteLength").GetInt64().Should().Be(bytes.Length);
        using (var content = await client.GetAsync($"{path}/media/{mediaId}/content"))
        {
            content.StatusCode.Should().Be(HttpStatusCode.OK, "the design export fetch()es this route into a blob");
            content.Headers.CacheControl!.NoStore.Should().BeTrue();
            (await content.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
        }

        var draft = await Data(await client.PostAsJsonAsync($"{path}/drafts", new
        {
            brandId = brand, title = "Files", caption = "Caption", language = "en", destination = "manual", sourceNotes = "", mediaIds = new[] { mediaId }
        }));
        var draftId = draft.GetProperty("id").GetGuid();
        await Data(await client.PostAsJsonAsync($"{path}/drafts/{draftId}/request-review", new { expectedRevision = 1 }));
        await Data(await client.PostAsJsonAsync($"{path}/drafts/{draftId}/approve", new { expectedRevision = 1 }));
        var key = Guid.NewGuid();
        var exports = await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ =>
            await Data(await client.PostAsJsonAsync($"{path}/drafts/{draftId}/exports", new { expectedRevision = 1, idempotencyKey = key }))));
        exports.Select(export => export.GetProperty("id").GetGuid()).Distinct().Should().ContainSingle();
        var exportId = exports[0].GetProperty("id").GetGuid();
        files.Uploads.Should().Be(2, "one image and one ZIP, whatever the retries");

        var zipId = (string)(await RowAsync("SELECT file_id FROM preparation.exports WHERE organization_id=$1 AND id=$2 AND bytes IS NULL",
            owner.OrganizationId, exportId))[0]!;
        var zip = files.Model(zipId);
        zip.Visibility.Should().Be(FileVisibility.Private);
        zip.Name.Should().Be($"manual-content-{exportId:D}.zip");
        files.ReferencesOf(zipId).Should().Contain(new FileReference("desk.preparation-export", $"{owner.OrganizationId:D}/{exportId:D}"));
        using (var archive = new ZipArchive(new MemoryStream(files.Content(zipId)), ZipArchiveMode.Read))
            (await ReadEntryAsync(archive, $"media/{mediaId:D}.png")).Should().Equal(bytes, "the package reads the image back from Files");
        using var conflict = await client.PostAsJsonAsync($"{path}/drafts/{draftId}/exports", new { expectedRevision = 2, idempotencyKey = key });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict, "the request hash still guards the key");

        using var download = await client.GetAsync(exports[0].GetProperty("downloadUrl").GetString());
        download.StatusCode.Should().Be(HttpStatusCode.Redirect);
        download.Headers.Location.Should().Be(TestFiles.Link(zipId, null, FileDisposition.Attachment));
        download.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task CommerceAttach_ReferencesThePreparationFile_AndPublicMediaRedirects()
    {
        var owner = await fixture.CreateOwnerAsync();
        var files = new TestFiles();
        await using var host = Host(files);
        using var client = await fixture.LoginAsync(host, owner);
        var path = Organization(owner);
        var brand = await BrandAsync(client, path);
        var commerce = $"{path}/brands/{brand}/commerce";
        var slug = "files-" + Guid.NewGuid().ToString("N");
        await Data(await client.PutAsJsonAsync(commerce, new { expectedRevision = 0, snapshot = new CommerceSnapshot { Slug = slug, Identity = new BrandIdentity { Name = "Files fixture" } } }));
        var bytes = Png(SKColors.Olive, 30, 30);
        var sha = Sha(bytes);
        var mediaId = (await UploadMediaAsync(client, path, bytes)).GetProperty("id").GetGuid();
        var image = files.Of("desk").Single();

        var attached = await Data(await client.PostAsJsonAsync($"{commerce}/media",
            new { expectedRevision = 1, mediaId, provenance = new { kind = "photo", approvedForPublication = true } }));
        files.Uploads.Should().Be(1, "the brand references the preparation file instead of copying its bytes");
        files.ReferencesOf(image.Id.ToString()).Should().Contain(new FileReference("desk.commerce-media", $"{brand:D}/{sha}"));
        (await RowAsync("SELECT bytes IS NULL, file_id FROM commerce.media WHERE brand_id=$1 AND sha256=$2", brand, sha))
            .Should().Equal(true, image.Id.ToString());
        using (var member = await client.GetAsync($"{commerce}/media/{sha}"))
        {
            member.StatusCode.Should().Be(HttpStatusCode.OK, "the design export fetch()es this route into a blob");
            (await member.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
        }

        var snapshot = JsonSerializer.Deserialize<CommerceSnapshot>(attached.GetProperty("snapshot"), new JsonSerializerOptions(JsonSerializerDefaults.Web))! with
        {
            Logos = new Dictionary<string, LogoVersion>
            {
                ["master"] = new("logo-1", "master", sha, "logo.png", "image/png", bytes.Length, 30, 30, false, "approved", null, null, DateTimeOffset.UtcNow)
            }
        };
        await Data(await client.PutAsJsonAsync(commerce, new { expectedRevision = 2, snapshot }));
        await Data(await client.PostAsJsonAsync($"{commerce}/releases", new { expectedRevision = 3, note = "Files logo" }));
        using var anonymous = DeskTestHost.CreateClient(host);
        using var published = await anonymous.GetAsync($"/api/public/v1/brands/{slug}/media/{sha}");
        published.StatusCode.Should().Be(HttpStatusCode.Redirect);
        published.Headers.Location.Should().Be(TestFiles.Link(image.Id, null));
        published.Headers.CacheControl!.NoStore.Should().BeTrue("the route allowed no caching before Files");
    }

    [Fact]
    public async Task CatalogueEditionArtifacts_LandInFilesNamedForDownload_AndPublicAssetsRedirect()
    {
        var owner = await fixture.CreateOwnerAsync();
        var files = new TestFiles();
        await using var host = Host(files);
        using var client = await fixture.LoginAsync(host, owner);
        var path = Organization(owner);
        var brand = await BrandAsync(client, path);
        var commerce = $"{path}/brands/{brand}/commerce";
        var slug = "edition-" + Guid.NewGuid().ToString("N");
        await Data(await client.PutAsJsonAsync(commerce, new { expectedRevision = 0, snapshot = new CommerceSnapshot { Slug = slug, Identity = new BrandIdentity { Name = "Edition fixture" } } }));
        var design = (await Data(await client.PostAsJsonAsync($"{path}/design-documents", new
        {
            kind = "catalogue", title = "Files edition",
            snapshot = new { schemaVersion = 1, kind = "catalogue", locale = "en", templateId = "fixture-template",
                pages = new[] { new { id = "cover", title = "Cover", width = 210, height = 297, productIds = Array.Empty<string>(), elements = Array.Empty<object>() } } }
        }))).GetProperty("id").GetGuid();
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj <</Type /Catalog>> endobj\n%%EOF\n");
        var png = Png(SKColors.Beige, 210, 297);
        using var bundle = new MultipartFormDataContent();
        bundle.Add(new StringContent(JsonSerializer.Serialize(new { expectedRevision = 1, sourceDesignId = design, sourceRevision = 1 })), "metadata");
        bundle.Add(new ByteArrayContent(pdf), "pdf", "catalogue.pdf");
        bundle.Add(new ByteArrayContent(png), "page-cover.png", "cover.png");
        bundle.Add(new ByteArrayContent(pdf), "page-cover.pdf", "cover.pdf");

        await Data(await client.PostAsync($"{commerce}/catalogue-editions", bundle));
        files.Uploads.Should().Be(2, "the catalogue PDF and the cover PDF are the same bytes");
        var stored = files.Of("desk").ToDictionary(file => file.Sha256);
        stored[Sha(pdf)].Name.Should().Be($"{slug}-en-catalogue.pdf");
        stored[Sha(png)].Name.Should().Be($"{slug}-en-cover.png");
        files.ReferencesOf(stored[Sha(pdf)].Id.ToString()).Should().Contain(new FileReference("desk.commerce-media", $"{brand:D}/{Sha(pdf)}"));
        (await RowAsync("SELECT count(*) FROM commerce.media WHERE brand_id=$1 AND bytes IS NULL AND file_id IS NOT NULL", brand)).Should().Equal(2L);

        await Data(await client.PostAsJsonAsync($"{commerce}/catalogue-editions/en/publish", new { expectedRevision = 2, note = "Reviewed" }));
        using var anonymous = DeskTestHost.CreateClient(host);
        var view = (await anonymous.GetFromJsonAsync<JsonElement>($"/api/public/v1/brands/{slug}/catalogue?locale=en")).GetProperty("edition");
        using var download = await anonymous.GetAsync(view.GetProperty("pdfUrl").GetString() + "&download=1");
        download.StatusCode.Should().Be(HttpStatusCode.Redirect);
        download.Headers.Location.Should().Be(TestFiles.Link(stored[Sha(pdf)].Id, null, FileDisposition.Attachment));
        download.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var page = await anonymous.GetAsync(view.GetProperty("pages")[0].GetProperty("pngUrl").GetString());
        page.Headers.Location.Should().Be(TestFiles.Link(stored[Sha(png)].Id, null));
    }

    [Fact]
    public async Task SourcingMedia_LandsInFilesOnce_WithoutThumbnailBytes_AndRoutesRedirect()
    {
        var owner = await fixture.CreateOwnerAsync();
        var files = new TestFiles();
        await using var host = Host(files);
        using var client = await fixture.LoginAsync(host, owner);
        var sourcing = $"{Organization(owner)}/sourcing";
        var bytes = Png(SKColors.CornflowerBlue, 900, 600);
        var sha = Sha(bytes);

        var first = await Data(await UploadSourcingAsync(client, sourcing, bytes, "photo.png"));
        var again = await Data(await UploadSourcingAsync(client, sourcing, bytes, "again.png"));
        again.GetProperty("duplicate").GetBoolean().Should().BeTrue();
        files.Uploads.Should().Be(1);
        var media = first.GetProperty("media");
        var mediaId = media.GetProperty("id").GetGuid();
        media.GetProperty("hasThumbnail").GetBoolean().Should().BeTrue("Files serves the thumbnail as a variant");
        var original = files.Of("desk").Single();
        original.Name.Should().Be($"{sha[..16]}.png");
        files.ReferencesOf(original.Id.ToString()).Should().Contain(new FileReference("desk.sourcing-media", $"{owner.OrganizationId:D}/{sha}"));
        (await RowAsync("SELECT bytes IS NULL, thumbnail IS NULL, file_id FROM sourcing.media_contents WHERE organization_id=$1 AND sha256=$2",
            owner.OrganizationId, sha)).Should().Equal(true, true, original.Id.ToString());

        using var content = await client.GetAsync($"{sourcing}/media/{mediaId}/content");
        content.StatusCode.Should().Be(HttpStatusCode.Redirect);
        content.Headers.Location.Should().Be(TestFiles.Link(original.Id, null));
        content.Headers.CacheControl!.NoStore.Should().BeTrue();
        using var thumbnail = await client.GetAsync($"{sourcing}/media/{mediaId}/thumbnail");
        thumbnail.StatusCode.Should().Be(HttpStatusCode.Redirect);
        thumbnail.Headers.Location.Should().Be(TestFiles.Link(original.Id, 480));
        using var foreign = await DeskTestHost.CreateClient(host).GetAsync($"{sourcing}/media/{mediaId}/content");
        foreign.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "membership still decides before any link is issued");
    }

    [Fact]
    public async Task RowsStoredBeforeFiles_ReadTheirBytes_UntilTheBootBackfillFillsTheirIds()
    {
        // Files off: every kind keeps its bytes in PostgreSQL, exactly as before.
        var owner = await fixture.CreateOwnerAsync();
        var path = Organization(owner);
        var brand = await fixture.Service.CreateBrandAsync(owner, "Legacy fixture", "UTC", Token);
        var image = Png(SKColors.Maroon, 20, 20);
        var media = await fixture.Service.CreateMediaAsync(owner, "legacy.png", "Fixture", image, Token);
        var draft = await fixture.Service.SaveDraftAsync(owner, null, null,
            new DraftInput(brand.Id, "Legacy", "Caption", "en", "manual", "", [media.Id]), Token);
        await fixture.Service.ReviewAsync(owner, draft.Id, 1, ReviewAction.Request, null, Token);
        await fixture.Service.ReviewAsync(owner, draft.Id, 1, ReviewAction.Approve, null, Token);
        var export = await fixture.Service.CreateExportAsync(owner, draft.Id, 1, Guid.NewGuid(), Token);
        var zip = await fixture.Service.GetExportContentAsync(owner, export.Id, Token);
        Guid sourcingId;
        var photo = Png(SKColors.Peru, 600, 400);
        using (var legacy = await fixture.LoginAsync(owner))
        {
            await Data(await legacy.PutAsJsonAsync($"{path}/brands/{brand.Id}/commerce", new { expectedRevision = 0, snapshot = new CommerceSnapshot { Slug = "legacy-" + Guid.NewGuid().ToString("N"), Identity = new BrandIdentity { Name = "Legacy" } } }));
            await Data(await legacy.PostAsJsonAsync($"{path}/brands/{brand.Id}/commerce/media",
                new { expectedRevision = 1, mediaId = media.Id, provenance = new { kind = "photo", approvedForPublication = false } }));
            sourcingId = (await Data(await UploadSourcingAsync(legacy, $"{path}/sourcing", photo, "legacy.png"))).GetProperty("media").GetProperty("id").GetGuid();
        }
        (await RowAsync("SELECT count(*) FROM preparation.media m JOIN preparation.exports e ON e.organization_id=m.organization_id WHERE m.organization_id=$1 AND m.bytes IS NOT NULL AND m.file_id IS NULL AND e.bytes IS NOT NULL AND e.file_id IS NULL",
            owner.OrganizationId)).Should().Equal(1L);
        (await RowAsync("SELECT bytes IS NOT NULL, file_id FROM commerce.media WHERE brand_id=$1", brand.Id)).Should().Equal(true, null);
        (await RowAsync("SELECT thumbnail IS NOT NULL, file_id FROM sourcing.media_contents WHERE organization_id=$1", owner.OrganizationId)).Should().Equal(true, null);

        // Files on, nothing moved yet: every route reads the PostgreSQL bytes.
        await using (var unmigrated = Host(new TestFiles()))
        {
            using var client = await fixture.LoginAsync(unmigrated, owner);
            (await (await client.GetAsync($"{path}/media/{media.Id}/content")).Content.ReadAsByteArrayAsync()).Should().Equal(image);
            using var download = await client.GetAsync(export.DownloadUrl);
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            (await download.Content.ReadAsByteArrayAsync()).Should().Equal(zip);
            (await (await client.GetAsync($"{path}/brands/{brand.Id}/commerce/media/{media.Sha256}")).Content.ReadAsByteArrayAsync()).Should().Equal(image);
            using var original = await client.GetAsync($"{path}/sourcing/media/{sourcingId}/content");
            original.StatusCode.Should().Be(HttpStatusCode.OK);
            (await original.Content.ReadAsByteArrayAsync()).Should().Equal(photo);
            using var thumbnail = await client.GetAsync($"{path}/sourcing/media/{sourcingId}/thumbnail");
            thumbnail.StatusCode.Should().Be(HttpStatusCode.OK);
            thumbnail.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        }

        // The migration moved each row under its manifest reference; a file with the same bytes for another record stays apart.
        var moved = new TestFiles();
        var decoy = moved.Add(image, "desk", new FileReference("desk.preparation-media", $"{Guid.NewGuid():D}/{Guid.NewGuid():D}"));
        var movedImage = moved.Add(image, "desk", new FileReference("desk.preparation-media", $"{owner.OrganizationId:D}/{media.Id:D}"));
        var movedZip = moved.Add(zip, "desk", new FileReference("desk.preparation-export", $"{owner.OrganizationId:D}/{export.Id:D}"), "application/zip");
        var movedBrand = moved.Add(image, "desk", new FileReference("desk.commerce-media", $"{brand.Id:D}/{media.Sha256}"));
        var movedPhoto = moved.Add(photo, "desk", new FileReference("desk.sourcing-media", $"{owner.OrganizationId:D}/{Sha(photo)}"));
        await using (var migrated = Host(moved))
        {
            using var client = await fixture.LoginAsync(migrated, owner);
            (await RowAsync("SELECT file_id FROM preparation.media_files WHERE organization_id=$1 AND media_id=$2", owner.OrganizationId, media.Id))
                .Should().Equal(movedImage.Id.ToString());
            decoy.Id.Should().NotBe(movedImage.Id);
            (await RowAsync("SELECT file_id FROM preparation.export_files WHERE organization_id=$1 AND export_id=$2", owner.OrganizationId, export.Id))
                .Should().Equal(movedZip.Id.ToString());
            (await RowAsync("SELECT file_id FROM commerce.media WHERE brand_id=$1", brand.Id)).Should().Equal(movedBrand.Id.ToString());
            (await RowAsync("SELECT file_id FROM sourcing.media_contents WHERE organization_id=$1", owner.OrganizationId)).Should().Equal(movedPhoto.Id.ToString());
            // The immutable row itself never changes.
            (await RowAsync("SELECT file_id IS NULL, bytes IS NOT NULL FROM preparation.media WHERE organization_id=$1 AND id=$2", owner.OrganizationId, media.Id))
                .Should().Equal(true, true);

            using var download = await client.GetAsync(export.DownloadUrl);
            download.Headers.Location.Should().Be(TestFiles.Link(movedZip.Id, null, FileDisposition.Attachment));
            using var original = await client.GetAsync($"{path}/sourcing/media/{sourcingId}/content");
            original.Headers.Location.Should().Be(TestFiles.Link(movedPhoto.Id, null));
            using var thumbnail = await client.GetAsync($"{path}/sourcing/media/{sourcingId}/thumbnail");
            thumbnail.Headers.Location.Should().Be(TestFiles.Link(movedPhoto.Id, 480));
            (await (await client.GetAsync($"{path}/media/{media.Id}/content")).Content.ReadAsByteArrayAsync()).Should().Equal(image);
        }
    }

    [Fact]
    public async Task AnUnreachableFiles_SparesTheBoot_AndTheClientTakesDesksIdentityClient()
    {
        var owner = await fixture.CreateOwnerAsync();
        var waiting = await fixture.Service.CreateMediaAsync(owner, "waiting.png", "Fixture", Png(SKColors.Navy, 10, 10), Token);
        var down = new TestFiles { Outage = AppErrorFactory.ExternalUnavailable("WoW2 Files is down.") };
        down.Add(Png(SKColors.Navy, 10, 10), "desk", new FileReference("desk.preparation-media", $"{owner.OrganizationId:D}/{waiting.Id:D}"));
        await using (var unavailable = Host(down))
        {
            using var client = await fixture.LoginAsync(unavailable, owner);
            (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await RowAsync("SELECT count(*) FROM preparation.media_files WHERE organization_id=$1", owner.OrganizationId)).Should().Equal(0L);
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(Png(SKColors.Gold, 10, 10)), "file", "down.png");
            form.Add(new StringContent("Fixture"), "provenance");
            (await client.PostAsync($"{Organization(owner)}/media", form)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        // The real client, against a Files host nobody answers: the boot still completes.
        await using var real = Host(null, new() { ["Files:BaseUrl"] = TestFiles.Url, ["Identity:Client:ClientSecret"] = "test-secret" });
        using var anonymous = DeskTestHost.CreateClient(real);
        (await anonymous.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        var options = real.Services.GetRequiredService<IOptions<FilesClientOptions>>().Value;
        options.BaseAddress.Should().Be(new Uri(TestFiles.Url));
        options.Identity!.TokenEndpoint.Should().Be(new Uri("https://identity.example.test/connect/token"));
        options.Identity.ClientId.Should().Be("desk");
        // The BFF's own source (internal to the SDK): a person's request exchanges the token of their session.
        real.Services.GetRequiredService<IOAuth2SubjectTokenSource>().GetType().FullName
            .Should().Be("WoW.Two.Sdk.Beta.Be.Identity.Bff.Sessions.BffSubjectTokenSource");
    }

    private WebApiTestHost<ApiAssemblyMarker> Host(TestFiles? files, Dictionary<string, string?>? settings = null)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = fixture.ConnectionString,
            ["AllowedHosts"] = "localhost;127.0.0.1",
            ["Files:BaseUrl"] = files is null ? null : TestFiles.Url,
        };
        foreach (var (key, value) in settings ?? [])
            configuration[key] = value;
        var factory = new WebApiTestHost<ApiAssemblyMarker>
        {
            ConfigureHostHook = builder => builder.UseEnvironment(Environments.Development).UseIdentitySettings(),
            ConfigureServicesHook = services =>
            {
                DeskTestHost.Use(services);
                files?.Register(services);
            },
            ConfigureConfigurationHook = builder => builder.AddInMemoryCollection(configuration)
        };
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        return factory;
    }

    private static string Organization(OrganizationAccess access) => $"/api/organizations/{access.OrganizationId}";

    private static async Task<Guid> BrandAsync(HttpClient client, string path) =>
        (await Data(await client.PostAsJsonAsync($"{path}/brands", new { name = "Files fixture", timeZone = "UTC" }))).GetProperty("id").GetGuid();

    private static async Task<JsonElement> UploadMediaAsync(HttpClient client, string path, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "fixture.png");
        form.Add(new StringContent("Generated test pixels; not product facts"), "provenance");
        return await Data(await client.PostAsync($"{path}/media", form));
    }

    private static async Task<HttpResponseMessage> UploadSourcingAsync(HttpClient client, string path, byte[] bytes, string name)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", name);
        return await client.PostAsync($"{path}/media", form);
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
        }
    }

    /// <summary>Reads one row's columns, DBNull as null.</summary>
    private async Task<object?[]> RowAsync(string sql, params object[] values)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue(sql);
        return Enumerable.Range(0, reader.FieldCount).Select(index => reader.IsDBNull(index) ? null : reader.GetValue(index)).ToArray();
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] Png(SKColor color, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var output = image.Encode(SKEncodedImageFormat.Png, 100);
        return output.ToArray();
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchive archive, string path)
    {
        await using var input = archive.GetEntry(path)!.Open();
        using var output = new MemoryStream();
        await input.CopyToAsync(output);
        return output.ToArray();
    }
}
