using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises suppliers, contact routes, shared media and bundle import against real PostgreSQL and membership.</summary>
public sealed class SourcingTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [Fact]
    public async Task Suppliers_ShouldPersistRevisions_AndFilterBySearchAndStage()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);

        JsonElement created = await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers", new
        {
            name = "  Sun Woollen Industries ", stage = "contacted", country = "in", city = "Amritsar",
            website = "https://example.test/sun", notes = "First line\nSecond line"
        }));
        Guid id = created.GetProperty("id").GetGuid();
        created.GetProperty("name").GetString().Should().Be("Sun Woollen Industries");
        created.GetProperty("country").GetString().Should().Be("IN");
        created.GetProperty("revision").GetInt32().Should().Be(1);
        created.GetProperty("key").ValueKind.Should().Be(JsonValueKind.Null);
        await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers", new { name = "Kashmir Weaves", stage = "quoted" }));

        JsonElement updated = await DataAsync(await client.PutAsJsonAsync($"{path}/suppliers/{id}", new
        {
            expectedRevision = 1, name = "Sun Woollen Industries", stage = "replied", country = "IN", city = "Ludhiana",
            website = (string?)null, notes = ""
        }));
        updated.GetProperty("revision").GetInt32().Should().Be(2);
        updated.GetProperty("website").ValueKind.Should().Be(JsonValueKind.Null);

        using HttpResponseMessage stale = await client.PutAsJsonAsync($"{path}/suppliers/{id}", new
        {
            expectedRevision = 1, name = "Stale", stage = "lead"
        });
        await ProblemAsync(stale, HttpStatusCode.Conflict, "conflict");

        JsonElement byCity = await DataAsync(await client.GetAsync($"{path}/suppliers?query=ludh"));
        byCity.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Equal(id);
        JsonElement byStage = await DataAsync(await client.GetAsync($"{path}/suppliers?stage=quoted"));
        byStage.EnumerateArray().Select(item => item.GetProperty("name").GetString()).Should().Equal("Kashmir Weaves");
        JsonElement wildcard = await DataAsync(await client.GetAsync($"{path}/suppliers?query=%25"));
        wildcard.GetArrayLength().Should().Be(0);

        await ProblemAsync(await client.GetAsync($"{path}/suppliers?stage=signed"), HttpStatusCode.BadRequest, "validation");
        await ProblemAsync(await client.PostAsJsonAsync($"{path}/suppliers", new { name = " " }), HttpStatusCode.BadRequest, "validation");
        await ProblemAsync(await client.PostAsJsonAsync($"{path}/suppliers", new { name = "Bad site", website = "javascript:alert(1)" }),
            HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task Routes_ShouldKeepOnePrimary_NormalizeDestinations_AndSurvivePersonRemoval()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);
        Guid supplier = (await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers", new { name = "BMMS" }))).GetProperty("id").GetGuid();
        Guid other = (await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers", new { name = "Other" }))).GetProperty("id").GetGuid();
        JsonElement person = await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers/{supplier}/people", new { name = "Akshit Mehra", role = "Sales" }));
        Guid personId = person.GetProperty("id").GetGuid();
        Guid foreignPerson = (await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers/{other}/people", new { name = "Someone" })))
            .GetProperty("id").GetGuid();

        JsonElement whatsapp = await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers/{supplier}/routes", new
        {
            channel = "whatsapp", destination = "+91 (000) 000-0001", verified = true, isPrimary = true, personId
        }));
        whatsapp.GetProperty("destination").GetString().Should().Be("+910000000001");
        whatsapp.GetProperty("personId").GetGuid().Should().Be(personId);
        JsonElement email = await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers/{supplier}/routes", new
        {
            channel = "email", destination = "Sales@Example.TEST", isPrimary = true, sourceUrl = "https://example.test/contact"
        }));
        email.GetProperty("destination").GetString().Should().Be("sales@example.test");

        JsonElement detail = await DataAsync(await client.GetAsync($"{path}/suppliers/{supplier}"));
        detail.GetProperty("routes").EnumerateArray().Where(route => route.GetProperty("isPrimary").GetBoolean())
            .Select(route => route.GetProperty("channel").GetString()).Should().Equal("email");
        detail.GetProperty("routes")[0].GetProperty("channel").GetString().Should().Be("email");
        int whatsappRevision = detail.GetProperty("routes")[1].GetProperty("revision").GetInt32();
        whatsappRevision.Should().Be(2);

        await ProblemAsync(await client.PostAsJsonAsync($"{path}/suppliers/{supplier}/routes", new { channel = "email", destination = "sales@example.test" }),
            HttpStatusCode.Conflict, "conflict");
        await ProblemAsync(await client.PostAsJsonAsync($"{path}/suppliers/{supplier}/routes", new
        {
            channel = "phone", destination = "+910000000002", personId = foreignPerson
        }), HttpStatusCode.BadRequest, "validation");
        await ProblemAsync(await client.PostAsJsonAsync($"{path}/suppliers/{supplier}/routes", new { channel = "website", destination = "not a url" }),
            HttpStatusCode.BadRequest, "validation");
        await ProblemAsync(await client.PostAsJsonAsync($"{path}/suppliers/{supplier}/routes", new { channel = "pigeon", destination = "x" }),
            HttpStatusCode.BadRequest, "validation");

        // The same destination may serve another supplier (a shared route).
        await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers/{other}/routes", new { channel = "whatsapp", destination = "+910000000001" }));

        using HttpResponseMessage staleDelete = await client.DeleteAsync($"{path}/people/{personId}?expectedRevision=9");
        await ProblemAsync(staleDelete, HttpStatusCode.Conflict, "conflict");
        await DataAsync(await client.DeleteAsync($"{path}/people/{personId}?expectedRevision=1"));
        JsonElement afterRemoval = await DataAsync(await client.GetAsync($"{path}/suppliers/{supplier}"));
        afterRemoval.GetProperty("people").GetArrayLength().Should().Be(0);
        JsonElement keptRoute = afterRemoval.GetProperty("routes").EnumerateArray()
            .Single(route => route.GetProperty("channel").GetString() == "whatsapp");
        keptRoute.GetProperty("personId").ValueKind.Should().Be(JsonValueKind.Null);
        keptRoute.GetProperty("revision").GetInt32().Should().Be(3);

        Guid emailId = email.GetProperty("id").GetGuid();
        await DataAsync(await client.DeleteAsync($"{path}/routes/{emailId}?expectedRevision=1"));
        (await DataAsync(await client.GetAsync($"{path}/suppliers/{supplier}"))).GetProperty("routes").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Membership_ShouldHideForeignSuppliers_AndKeepViewersReadOnly()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        OrganizationAccess stranger = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        using HttpClient strangerClient = await fixture.LoginAsync(stranger);
        Guid supplier = (await DataAsync(await client.PostAsJsonAsync($"{Path(owner)}/suppliers", new { name = "Private" })))
            .GetProperty("id").GetGuid();

        (await strangerClient.GetAsync($"{Path(owner)}/suppliers")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProblemAsync(await strangerClient.GetAsync($"{Path(stranger)}/suppliers/{supplier}"), HttpStatusCode.NotFound, "not-found");
        (await DataAsync(await strangerClient.GetAsync($"{Path(stranger)}/suppliers"))).GetArrayLength().Should().Be(0);
        await ProblemAsync(await strangerClient.PostAsync($"{Path(stranger)}/media/assign",
            JsonContent.Create(new { mediaIds = new[] { Guid.NewGuid() }, supplierId = supplier })), HttpStatusCode.BadRequest, "validation");

        await fixture.SetRoleAsync(owner, "viewer");
        (await DataAsync(await client.GetAsync($"{Path(owner)}/suppliers/{supplier}"))).GetProperty("name").GetString().Should().Be("Private");
        await ProblemAsync(await client.PostAsJsonAsync($"{Path(owner)}/suppliers", new { name = "Blocked" }), HttpStatusCode.Forbidden, "forbidden");
        await ProblemAsync(await UploadAsync(client, owner, Png(SKColors.Red), "red.png"), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Media_ShouldDeduplicate_PreviewImages_AndMoveBetweenInboxAndSupplier()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);
        Guid supplier = (await DataAsync(await client.PostAsJsonAsync($"{path}/suppliers", new { name = "Lucky" }))).GetProperty("id").GetGuid();
        byte[] png = Png(SKColors.CornflowerBlue, 900, 600);

        JsonElement first = await DataAsync(await UploadAsync(client, owner, png, "../../photo:1.png"));
        JsonElement media = first.GetProperty("media");
        Guid mediaId = media.GetProperty("id").GetGuid();
        first.GetProperty("duplicate").GetBoolean().Should().BeFalse();
        media.GetProperty("supplierId").ValueKind.Should().Be(JsonValueKind.Null);
        media.GetProperty("sha256").GetString().Should().Be(Convert.ToHexStringLower(SHA256.HashData(png)));
        media.GetProperty("contentType").GetString().Should().Be("image/png");
        media.GetProperty("width").GetInt32().Should().Be(900);
        media.GetProperty("height").GetInt32().Should().Be(600);
        media.GetProperty("hasThumbnail").GetBoolean().Should().BeTrue();
        media.GetProperty("capture").GetString().Should().Be("original");
        media.GetProperty("originalName").GetString().Should().Be("photo_1.png");
        media.GetProperty("hasLocation").GetBoolean().Should().BeFalse();

        JsonElement again = await DataAsync(await UploadAsync(client, owner, png, "same.png", supplier));
        again.GetProperty("duplicate").GetBoolean().Should().BeTrue();
        again.GetProperty("media").GetProperty("id").GetGuid().Should().Be(mediaId);
        again.GetProperty("media").GetProperty("supplierId").ValueKind.Should().Be(JsonValueKind.Null);

        using HttpResponseMessage original = await client.GetAsync($"{path}/media/{mediaId}/content");
        original.StatusCode.Should().Be(HttpStatusCode.OK);
        (await original.Content.ReadAsByteArrayAsync()).Should().Equal(png);
        original.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        using HttpResponseMessage thumbnail = await client.GetAsync($"{path}/media/{mediaId}/thumbnail");
        thumbnail.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        using (SKBitmap preview = SKBitmap.Decode(await thumbnail.Content.ReadAsByteArrayAsync()))
        {
            preview.Width.Should().Be(480);
            preview.Height.Should().Be(320);
        }

        (await DataAsync(await client.GetAsync($"{path}/media/summary"))).GetProperty("unassigned").GetInt32().Should().Be(1);
        (await DataAsync(await client.GetAsync($"{path}/media?unassigned=true"))).GetArrayLength().Should().Be(1);
        JsonElement assigned = await DataAsync(await client.PostAsJsonAsync($"{path}/media/assign", new { mediaIds = new[] { mediaId, mediaId }, supplierId = supplier }));
        assigned.GetProperty("updated").GetInt32().Should().Be(1);
        (await DataAsync(await client.GetAsync($"{path}/media?unassigned=true"))).GetArrayLength().Should().Be(0);
        JsonElement summary = (await DataAsync(await client.GetAsync($"{path}/suppliers"))).EnumerateArray().Single();
        summary.GetProperty("mediaCount").GetInt32().Should().Be(1);
        summary.GetProperty("coverMediaId").GetGuid().Should().Be(mediaId);

        JsonElement captioned = await DataAsync(await client.PutAsJsonAsync($"{path}/media/{mediaId}", new
        {
            expectedRevision = 2, supplierId = supplier, capture = "screenshot", caption = "Peach floral, near B"
        }));
        captioned.GetProperty("revision").GetInt32().Should().Be(3);
        captioned.GetProperty("capture").GetString().Should().Be("screenshot");
        await ProblemAsync(await client.PutAsJsonAsync($"{path}/media/{mediaId}", new { expectedRevision = 2, supplierId = supplier, capture = "original", caption = "" }),
            HttpStatusCode.Conflict, "conflict");
        await ProblemAsync(await client.PutAsJsonAsync($"{path}/media/{mediaId}", new { expectedRevision = 3, supplierId = Guid.NewGuid(), capture = "original", caption = "" }),
            HttpStatusCode.BadRequest, "validation");

        await DataAsync(await client.DeleteAsync($"{path}/media/{mediaId}?expectedRevision=3"));
        await ProblemAsync(await client.GetAsync($"{path}/media/{mediaId}/content"), HttpStatusCode.NotFound, "not-found");
        (await DataAsync(await UploadAsync(client, owner, png, "returned.png"))).GetProperty("duplicate").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Media_ShouldAcceptPdfAndVideo_FlagNearDuplicates_AndRejectOtherContent()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);

        JsonElement pdf = (await DataAsync(await UploadAsync(client, owner, Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF\n"), "price list.pdf")))
            .GetProperty("media");
        pdf.GetProperty("contentType").GetString().Should().Be("application/pdf");
        pdf.GetProperty("hasThumbnail").GetBoolean().Should().BeFalse();
        await ProblemAsync(await client.GetAsync($"{path}/media/{pdf.GetProperty("id").GetGuid()}/thumbnail"), HttpStatusCode.NotFound, "not-found");
        byte[] video = [0, 0, 0, 24, .. "ftypmp42"u8, 0, 0, 0, 0, .. "mp42isom"u8];
        JsonElement mp4 = (await DataAsync(await UploadAsync(client, owner, video, "rack.mp4"))).GetProperty("media");
        mp4.GetProperty("contentType").GetString().Should().Be("video/mp4");
        using var range = new HttpRequestMessage(HttpMethod.Get, $"{path}/media/{mp4.GetProperty("id").GetGuid()}/content");
        range.Headers.Range = new RangeHeaderValue(0, 3);
        using HttpResponseMessage partial = await client.SendAsync(range);
        partial.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        (await partial.Content.ReadAsByteArrayAsync()).Should().Equal(video[..4]);

        byte[] gradient = Gradient(horizontal: true, marker: false);
        Guid gradientId = (await DataAsync(await UploadAsync(client, owner, gradient, "gradient.png"))).GetProperty("media").GetProperty("id").GetGuid();
        JsonElement near = await DataAsync(await UploadAsync(client, owner, Gradient(horizontal: true, marker: true), "gradient-copy.png"));
        near.GetProperty("duplicate").GetBoolean().Should().BeFalse();
        near.GetProperty("nearDuplicateIds").EnumerateArray().Select(item => item.GetGuid()).Should().Contain(gradientId);
        JsonElement distinct = await DataAsync(await UploadAsync(client, owner, Gradient(horizontal: false, marker: false), "vertical.png"));
        distinct.GetProperty("nearDuplicateIds").EnumerateArray().Select(item => item.GetGuid()).Should().NotContain(gradientId);

        await ProblemAsync(await UploadAsync(client, owner, Encoding.UTF8.GetBytes("plain text"), "notes.txt"), HttpStatusCode.BadRequest, "validation");
        await ProblemAsync(await UploadAsync(client, owner, [], "empty.png"), HttpStatusCode.RequestEntityTooLarge, "too-large");
    }

    [Fact]
    public async Task Import_ShouldValidateFirst_BeIdempotent_AndNeverOverwriteDeskEdits()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);
        string directory = Directory.CreateTempSubdirectory("sourcing-import-").FullName;
        try
        {
            byte[] photo = Png(SKColors.Peru, 40, 30);
            await File.WriteAllBytesAsync(System.IO.Path.Combine(directory, "swi-01.png"), photo);
            await File.WriteAllBytesAsync(System.IO.Path.Combine(directory, "bad.png"), Png(SKColors.Navy, 20, 20));
            string bundle = await WriteBundleAsync(directory, "quoted", Convert.ToHexStringLower(SHA256.HashData(photo)));

            SourcingImportReport dryRun = await ImportAsync(owner, bundle, dryRun: true);
            dryRun.Suppliers.Created.Should().Be(2);
            dryRun.People.Created.Should().Be(1);
            dryRun.Routes.Created.Should().Be(3);
            dryRun.Media.Created.Should().Be(1);
            dryRun.Media.Failed.Should().Be(1);
            dryRun.Failures.Single().Reason.Should().Contain("SHA-256");
            (await DataAsync(await client.GetAsync($"{path}/suppliers"))).GetArrayLength().Should().Be(0);

            SourcingImportReport imported = await ImportAsync(owner, bundle, dryRun: false);
            imported.Suppliers.Created.Should().Be(2);
            imported.Media.Created.Should().Be(1);
            JsonElement suppliers = await DataAsync(await client.GetAsync($"{path}/suppliers"));
            JsonElement swi = suppliers.EnumerateArray().Single(item => item.GetProperty("key").GetString() == "swi");
            swi.GetProperty("stage").GetString().Should().Be("quoted");
            swi.GetProperty("mediaCount").GetInt32().Should().Be(1);
            swi.GetProperty("primaryRoute").GetProperty("destination").GetString().Should().Be("+910000000001");
            Guid swiId = swi.GetProperty("id").GetGuid();
            JsonElement detail = await DataAsync(await client.GetAsync($"{path}/suppliers/{swiId}"));
            Guid personId = detail.GetProperty("people")[0].GetProperty("id").GetGuid();
            detail.GetProperty("routes")[0].GetProperty("personId").GetGuid().Should().Be(personId);
            JsonElement media = (await DataAsync(await client.GetAsync($"{path}/media?supplierId={swiId}")))[0];
            media.GetProperty("capture").GetString().Should().Be("screenshot");
            media.GetProperty("sourceLabel").GetString().Should().Be("WhatsApp screenshot 2026-09-29");
            media.GetProperty("receivedAt").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 9, 29, 6, 54, 0, TimeSpan.Zero));

            await DataAsync(await client.PutAsJsonAsync($"{path}/suppliers/{swiId}", new
            {
                expectedRevision = 1, name = "SWI (edited in Desk)", stage = "dropped", country = "IN"
            }));
            SourcingImportReport repeated = await ImportAsync(owner, bundle, dryRun: false);
            repeated.Suppliers.Existing.Should().Be(2);
            repeated.People.Existing.Should().Be(1);
            repeated.Routes.Existing.Should().Be(3);
            repeated.Media.Existing.Should().Be(1);
            (repeated.Suppliers.Created + repeated.People.Created + repeated.Routes.Created + repeated.Media.Created).Should().Be(0);
            JsonElement kept = await DataAsync(await client.GetAsync($"{path}/suppliers/{swiId}"));
            kept.GetProperty("name").GetString().Should().Be("SWI (edited in Desk)");
            kept.GetProperty("stage").GetString().Should().Be("dropped");

            string qualified = await WriteBundleAsync(directory, "qualified", Convert.ToHexStringLower(SHA256.HashData(photo)));
            Func<Task> rejected = () => ImportAsync(owner, qualified, dryRun: false);
            (await rejected.Should().ThrowAsync<PreparationException>()).Which.Message.Should().Contain("qualified");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private async Task<SourcingImportReport> ImportAsync(OrganizationAccess owner, string bundle, bool dryRun)
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SourcingImportService>().ImportAsync(owner, bundle, dryRun, Token);
    }

    private static async Task<string> WriteBundleAsync(string directory, string stage, string sha256)
    {
        string file = System.IO.Path.Combine(directory, $"bundle-{stage}.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new
        {
            format = SourcingImportService.Format,
            source = "synthetic fixture",
            suppliers = new object[]
            {
                new
                {
                    key = "swi", name = "Sun Woollen Industries", stage, country = "IN", city = (string?)null,
                    website = "https://example.test/swi", notes = "Imported from a synthetic ledger.",
                    people = new[] { new { key = "navit", name = "Navit Example", role = (string?)null, notes = "" } },
                    routes = new object[]
                    {
                        new { channel = "whatsapp", destination = "+91 0000000001", verified = true, isPrimary = true, personKey = "navit" },
                        new { channel = "email", destination = "swi@example.test", verified = false, isPrimary = false }
                    }
                },
                new
                {
                    key = "kriti", name = "PL Industries", stage = "contacted", country = "IN",
                    routes = new[] { new { channel = "form", destination = "https://example.test/form", verified = false, isPrimary = true } }
                }
            },
            media = new object[]
            {
                new
                {
                    supplierKey = "swi", path = "swi-01.png", sha256, capture = "screenshot", originalName = "swi-01.png",
                    caption = "", sourceLabel = "WhatsApp screenshot 2026-09-29", sourceMessageId = (string?)null,
                    receivedAt = "2026-09-29T11:54:00+05:00"
                },
                new { supplierKey = "kriti", path = "bad.png", sha256 = new string('0', 64), capture = "original" }
            }
        }), Token);
        return file;
    }

    private static string Path(OrganizationAccess access) => $"/api/organizations/{access.OrganizationId}/sourcing";

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, OrganizationAccess access, byte[] bytes, string fileName, Guid? supplierId = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", fileName);
        if (supplierId is not null) form.Add(new StringContent(supplierId.Value.ToString()), "supplierId");
        return await client.PostAsync($"{Path(access)}/media", form);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using (response)
        {
            string body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
        }
    }

    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            string body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(status, body);
            JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().Should().Be(code);
        }
    }

    private static byte[] Png(SKColor color, int width = 1, int height = 1)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData output = image.Encode(SKEncodedImageFormat.Png, 100);
        return output.ToArray();
    }

    private static byte[] Gradient(bool horizontal, bool marker)
    {
        using var bitmap = new SKBitmap(96, 96);
        for (int y = 0; y < 96; y++)
        for (int x = 0; x < 96; x++)
        {
            byte value = (byte)((horizontal ? x : y) * 255 / 95);
            bitmap.SetPixel(x, y, new SKColor(value, (byte)(255 - value), 90));
        }
        if (marker) bitmap.SetPixel(10, 10, SKColors.White);
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData output = image.Encode(SKEncodedImageFormat.Png, 100);
        return output.ToArray();
    }
}
