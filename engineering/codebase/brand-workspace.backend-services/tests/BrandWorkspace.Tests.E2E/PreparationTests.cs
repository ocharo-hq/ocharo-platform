using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Preparation.Models;
using Npgsql;
using SkiaSharp;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Checks tenant boundaries, immutable snapshots, roles, and real PostgreSQL races.</summary>
public sealed class PreparationTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [Fact]
    public async Task Workflow_ShouldExportExactImmutableBytesAndCopy_AndPreservePackageAfterRevocation()
    {
        var owner = await fixture.CreateOwnerAsync();
        var (input, media, bytes) = await CreateInputAsync(owner);
        var draft = await fixture.Service.SaveDraftAsync(owner, null, null, input, Token);
        var beforeReview = () => fixture.Service.ReviewAsync(owner, draft.Id, 1, ReviewAction.Approve, null, Token);
        (await beforeReview.Should().ThrowAsync<PreparationException>()).Which.Code.Should().Be("conflict");
        draft = await ApproveAsync(owner, draft);
        var key = Guid.NewGuid();
        var exports = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            fixture.Service.CreateExportAsync(owner, draft.Id, 1, key, Token)));
        exports.Select(export => export.Id).Distinct().Should().ContainSingle();
        var export = exports[0];
        var package = await fixture.Service.GetExportContentAsync(owner, export.Id, Token);
        using (var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read))
        {
            archive.Entries.Select(entry => entry.FullName).Should().BeEquivalentTo(
                ["caption.txt", "manifest.json", $"media/{media.Id:D}.png"]);
            (await ReadEntryAsync(archive, "caption.txt")).Should().Equal(Encoding.UTF8.GetBytes(input.Caption));
            (await ReadEntryAsync(archive, $"media/{media.Id:D}.png")).Should().Equal(bytes);
            var manifest = JsonDocument.Parse(await ReadEntryAsync(archive, "manifest.json")).RootElement;
            manifest.GetProperty("publicationStatus").GetString().Should().Be("unverified");
            manifest.GetProperty("mediaIncluded").GetBoolean().Should().BeTrue();
            manifest.GetProperty("revision").GetInt32().Should().Be(1);
            manifest.GetProperty("approval").GetProperty("approvedBy").GetString().Should().Be(owner.UserId);
            manifest.GetProperty("media")[0].GetProperty("sha256").GetString().Should().Be(media.Sha256);
            manifest.GetProperty("operatorReceipt").GetProperty("reportedPublishedAt").ValueKind.Should().Be(JsonValueKind.Null);
        }
        await fixture.Service.ReviewAsync(owner, draft.Id, 1, ReviewAction.Revoke, "Replace the caption", Token);
        var newExport = () => fixture.Service.CreateExportAsync(owner, draft.Id, 1, Guid.NewGuid(), Token);
        (await newExport.Should().ThrowAsync<PreparationException>()).Which.Code.Should().Be("conflict");
        (await fixture.Service.CreateExportAsync(owner, draft.Id, 1, key, Token)).Id.Should().Be(export.Id);
        (await fixture.Service.GetExportContentAsync(owner, export.Id, Token)).Should().Equal(package);
        var misuse = () => fixture.Service.CreateExportAsync(owner, draft.Id, 2, key, Token);
        (await misuse.Should().ThrowAsync<PreparationException>()).Which.Code.Should().Be("conflict");
        using var client = await fixture.LoginAsync(owner);
        var downloaded = await client.GetAsync(export.DownloadUrl);
        downloaded.StatusCode.Should().Be(HttpStatusCode.OK);
        (await downloaded.Content.ReadAsByteArrayAsync()).Should().Equal(package);
    }

    [Fact]
    public async Task TenantScope_ShouldDenyForeignReferencesReadsExports_AndDatabaseAssociations()
    {
        var first = await fixture.CreateOwnerAsync();
        var second = await fixture.CreateOwnerAsync();
        var (input, media, _) = await CreateInputAsync(first);
        var draft = await ApproveAsync(first, await fixture.Service.SaveDraftAsync(first, null, null, input, Token));
        var export = await fixture.Service.CreateExportAsync(first, draft.Id, 1, Guid.NewGuid(), Token);
        Func<Task>[] forbidden =
        [
            () => fixture.Service.GetDraftAsync(second, draft.Id, Token),
            () => fixture.Service.GetMediaContentAsync(second, media.Id, Token),
            () => fixture.Service.GetExportContentAsync(second, export.Id, Token),
            () => fixture.Service.SaveDraftAsync(second, null, null, input, Token)
        ];
        foreach (var operation in forbidden)
            (await operation.Should().ThrowAsync<PreparationException>()).Which.Code.Should().Be("not-found");
        var ownBrand = await fixture.Service.CreateBrandAsync(second, "Other brand", "UTC", Token);
        var foreignMedia = () => fixture.Service.SaveDraftAsync(second, null, null, input with { BrandId = ownBrand.Id }, Token);
        (await foreignMedia.Should().ThrowAsync<PreparationException>()).Which.Code.Should().Be("not-found");
        await using var sql = fixture.DataSource.CreateCommand(
            "INSERT INTO preparation.draft_media(organization_id,draft_id,revision,position,media_id) VALUES($1,$2,1,0,$3)");
        sql.Parameters.AddWithValue(second.OrganizationId);
        sql.Parameters.AddWithValue(draft.Id);
        sql.Parameters.AddWithValue(media.Id);
        var invalidFk = () => sql.ExecuteNonQueryAsync();
        (await invalidFk.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        using var client = await fixture.LoginAsync(second);
        (await client.GetAsync($"/api/organizations/{first.OrganizationId}/drafts/{draft.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/organizations/{second.OrganizationId}/media/{media.Id}/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/organizations/{second.OrganizationId}/exports/{export.Id}/download")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Roles_ShouldEnforceLiveMembership_WithoutFakeHttpPrincipals()
    {
        var owner = await fixture.CreateOwnerAsync();
        var (input, _, _) = await CreateInputAsync(owner);
        var draft = await fixture.Service.SaveDraftAsync(owner, null, null, input, Token);
        using var client = await fixture.LoginAsync(owner);
        var path = $"/api/organizations/{owner.OrganizationId}";
        await fixture.SetRoleAsync(owner, "editor");
        (await client.PostAsJsonAsync($"{path}/drafts/{draft.Id}/request-review", new { expectedRevision = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"{path}/drafts/{draft.Id}/approve", new { expectedRevision = 1 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync($"{path}/brands", new { name = "Denied", timeZone = "UTC" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await fixture.SetRoleAsync(owner, "viewer");
        (await client.GetAsync($"{path}/drafts/{draft.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"{path}/drafts/{draft.Id}", new
        {
            input.BrandId, input.Title, input.Caption, input.Language, input.Destination, input.SourceNotes, input.MediaIds, expectedRevision = 1
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await fixture.SetRoleAsync(owner, null);
        (await client.GetAsync($"{path}/drafts/{draft.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConcurrentSaves_ShouldKeepOneWinner_AndHistoricalBrandAndContent()
    {
        var owner = await fixture.CreateOwnerAsync();
        var (input, _, _) = await CreateInputAsync(owner);
        var draft = await ApproveAsync(owner, await fixture.Service.SaveDraftAsync(owner, null, null, input, Token));
        var nextBrand = await fixture.Service.CreateBrandAsync(owner, "Second brand", "Asia/Tashkent", Token);
        var first = CaptureAsync(() => fixture.Service.SaveDraftAsync(owner, draft.Id, 1,
            input with { Title = "First edit", BrandId = nextBrand.Id }, Token));
        var second = CaptureAsync(() => fixture.Service.SaveDraftAsync(owner, draft.Id, 1,
            input with { Title = "Second edit", BrandId = nextBrand.Id }, Token));
        var results = await Task.WhenAll(first, second);
        results.Count(result => result.Error is null).Should().Be(1);
        results.Single(result => result.Error is not null).Error!.Code.Should().Be("conflict");
        var current = await fixture.Service.GetDraftAsync(owner, draft.Id, Token);
        current.Revision.Should().Be(2);
        current.Status.Should().Be("draft");
        current.Approval.Should().BeNull();
        current.RequestedReviewRevision.Should().BeNull();
        current.BrandId.Should().Be(nextBrand.Id);
        await using var history = fixture.DataSource.CreateCommand(
            "SELECT brand_id,snapshot->>'brandId',snapshot->>'caption' FROM preparation.draft_revisions WHERE organization_id=$1 AND draft_id=$2 AND revision=1");
        history.Parameters.AddWithValue(owner.OrganizationId);
        history.Parameters.AddWithValue(draft.Id);
        await using var reader = await history.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetGuid(0).Should().Be(input.BrandId);
        reader.GetString(1).Should().Be(input.BrandId.ToString());
        reader.GetString(2).Should().Be(input.Caption);
    }

    [Fact]
    public async Task ApprovalAndExportRaces_ShouldNeverApproveOrExportAnotherRevision()
    {
        var owner = await fixture.CreateOwnerAsync();
        var (input, _, _) = await CreateInputAsync(owner);
        for (var run = 0; run < 4; run++)
        {
            var draft = await fixture.Service.SaveDraftAsync(owner, null, null, input, Token);
            await fixture.Service.ReviewAsync(owner, draft.Id, 1, ReviewAction.Request, null, Token);
            await Task.WhenAll(
                CaptureAsync(() => fixture.Service.ReviewAsync(owner, draft.Id, 1, ReviewAction.Approve, null, Token)),
                CaptureAsync(() => fixture.Service.SaveDraftAsync(owner, draft.Id, 1, input with { Caption = "Changed" }, Token)));
            var current = await fixture.Service.GetDraftAsync(owner, draft.Id, Token);
            current.Revision.Should().Be(2);
            current.Approval.Should().BeNull();
            current = await ApproveAsync(owner, current);
            var exporting = CaptureAsync(() => fixture.Service.CreateExportAsync(owner, draft.Id, 2, Guid.NewGuid(), Token));
            var revoking = CaptureAsync(() => fixture.Service.ReviewAsync(owner, draft.Id, 2, ReviewAction.Revoke, "Concurrent revocation", Token));
            await Task.WhenAll(exporting, revoking);
            var exported = await exporting;
            if (exported.Error is null)
            {
                var bytes = await fixture.Service.GetExportContentAsync(owner, exported.Value!.Id, Token);
                using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                (await ReadEntryAsync(archive, "caption.txt")).Should().Equal(Encoding.UTF8.GetBytes("Changed"));
            }
            else exported.Error.Code.Should().Be("conflict");
            (await fixture.Service.GetDraftAsync(owner, draft.Id, Token)).Approval.Should().BeNull();
            var afterRevocation = () => fixture.Service.CreateExportAsync(owner, draft.Id, 2, Guid.NewGuid(), Token);
            (await afterRevocation.Should().ThrowAsync<PreparationException>()).Which.Code.Should().Be("conflict");
        }
    }

    [Fact]
    public async Task ExportKeys_ShouldBeUniqueAcrossDrafts_AndEmptyMediaMustStayExplicit()
    {
        var owner = await fixture.CreateOwnerAsync();
        var (input, _, _) = await CreateInputAsync(owner);
        input = input with { MediaIds = [] };
        var first = await ApproveAsync(owner, await fixture.Service.SaveDraftAsync(owner, null, null, input, Token));
        var second = await ApproveAsync(owner, await fixture.Service.SaveDraftAsync(owner, null, null, input, Token));
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(
            CaptureAsync(() => fixture.Service.CreateExportAsync(owner, first.Id, 1, key, Token)),
            CaptureAsync(() => fixture.Service.CreateExportAsync(owner, second.Id, 1, key, Token)));
        results.Count(result => result.Error is null).Should().Be(1);
        results.Single(result => result.Error is not null).Error!.Code.Should().Be("conflict");
        var export = results.Single(result => result.Error is null).Value!;
        var bytes = await fixture.Service.GetExportContentAsync(owner, export.Id, Token);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        archive.Entries.Should().HaveCount(2);
        var manifest = JsonDocument.Parse(await ReadEntryAsync(archive, "manifest.json")).RootElement;
        manifest.GetProperty("media").GetArrayLength().Should().Be(0);
        await using var mutate = fixture.DataSource.CreateCommand(
            "UPDATE preparation.exports SET bytes=$3 WHERE organization_id=$1 AND id=$2");
        mutate.Parameters.AddWithValue(owner.OrganizationId);
        mutate.Parameters.AddWithValue(export.Id);
        mutate.Parameters.AddWithValue(new byte[] { 1, 2, 3 });
        var mutateSnapshot = () => mutate.ExecuteNonQueryAsync();
        (await mutateSnapshot.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55000");
    }

    [Fact]
    public async Task Upload_ShouldValidateRealMultipartBytes_AndReturnPrivateExactContent()
    {
        var owner = await fixture.CreateOwnerAsync();
        using var client = await fixture.LoginAsync(owner);
        var path = $"/api/organizations/{owner.OrganizationId}/media";
        var bytes = ImageBytes();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "real.png");
        form.Add(new StringContent("Fixture-created image; not product evidence"), "provenance");
        var response = await client.PostAsync(path, form);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var media = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        media.GetProperty("sha256").GetString().Should().Be(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var content = await client.GetAsync($"{path}/{media.GetProperty("id").GetString()}/content");
        content.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        content.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await content.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
        using var malformed = new MultipartFormDataContent();
        malformed.Add(new ByteArrayContent("not a PNG"u8.ToArray()), "file", "invalid.png");
        malformed.Add(new StringContent("Fixture"), "provenance");
        (await client.PostAsync(path, malformed)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Upload_ShouldRejectTruncatedMultipart_WithoutPersistingMedia(bool truncateImage)
    {
        var owner = await fixture.CreateOwnerAsync();
        using var client = await fixture.LoginAsync(owner);
        var path = $"/api/organizations/{owner.OrganizationId}/media";
        const string boundary = "preparation-truncated-fixture";
        using var complete = new MultipartFormDataContent(boundary);
        complete.Add(new StringContent("Test fixture"), "provenance");
        complete.Add(new ByteArrayContent(ImageBytes()), "file", "fixture.png");
        var bytes = await complete.ReadAsByteArrayAsync();
        var closingLength = Encoding.ASCII.GetByteCount($"\r\n--{boundary}--\r\n");
        var length = bytes.Length - closingLength - (truncateImage ? 20 : 0);
        using var truncated = new ByteArrayContent(bytes[..length]);
        truncated.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(
            $"multipart/form-data; boundary={boundary}");
        using var response = await client.PostAsync(path, truncated);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("validation");
        (await fixture.Service.GetMediaAsync(owner, Token)).Should().BeEmpty();
    }

    private async Task<(DraftInput Input, MediaAsset Media, byte[] Bytes)> CreateInputAsync(OrganizationAccess owner)
    {
        var brand = await fixture.Service.CreateBrandAsync(owner, "Fixture stationery", "UTC", Token);
        var bytes = ImageBytes();
        var media = await fixture.Service.CreateMediaAsync(owner, "fixture.png", "Generated test pixels; not product facts", bytes, Token);
        var input = new DraftInput(brand.Id, "Operator-authored title", "Exact caption\nSecond line: O‘zbekcha.", "uz-Latn",
            "manual", "User-supplied reference; no fetched facts.", [media.Id]);
        return (input, media, bytes);
    }

    private async Task<Draft> ApproveAsync(OrganizationAccess owner, Draft draft)
    {
        await fixture.Service.ReviewAsync(owner, draft.Id, draft.Revision, ReviewAction.Request, null, Token);
        return await fixture.Service.ReviewAsync(owner, draft.Id, draft.Revision, ReviewAction.Approve, null, Token);
    }

    private static byte[] ImageBytes()
    {
        using var bitmap = new SKBitmap(1, 1);
        bitmap.Erase(new SKColor(10, 60, 200, 255));
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

    private static async Task<(T? Value, PreparationException? Error)> CaptureAsync<T>(Func<Task<T>> action)
    {
        try { return (await action(), null); }
        catch (PreparationException exception) { return (default, exception); }
    }
}
