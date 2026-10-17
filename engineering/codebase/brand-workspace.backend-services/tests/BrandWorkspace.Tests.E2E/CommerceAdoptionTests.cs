using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Application.Access;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises authenticated adoption against synthetic offline donor databases and real tenant persistence.</summary>
public sealed class CommerceAdoptionTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-organization")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Adoption_RejectsMissingOrInvalidBindingBeforeReadingSource(string? binding)
    {
        var owner = await fixture.CreateOwnerAsync();
        using var client = await fixture.LoginAsync(owner);
        var brand = await CreateBrand(client, owner);
        var config = fixture.Factory.Services.GetRequiredService<IConfiguration>();
        config["Commerce:AdoptionOrganizationId"] = binding;
        // This source cannot be read. Authorization must fail before the importer reports a file error.
        config["Commerce:SourceRoot"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using var response = await client.PostAsJsonAsync(PathFor(owner, brand), new { slug = "synthetic-adoption" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await client.GetFromJsonAsync<JsonElement>(CommercePath(owner, brand))).GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Adoption_OnlyConfiguredOwnerCanImportGlobalDonorData()
    {
        var intended = await fixture.CreateOwnerAsync();
        var other = await fixture.CreateOwnerAsync();
        using var intendedClient = await fixture.LoginAsync(intended);
        using var otherClient = await fixture.LoginAsync(other);
        var intendedBrand = await CreateBrand(intendedClient, intended);
        var otherBrand = await CreateBrand(otherClient, other);
        var root = Path.Combine(Path.GetTempPath(), "commerce-adoption-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            await CreateDonor(root);
            var config = fixture.Factory.Services.GetRequiredService<IConfiguration>();
            config["Commerce:AdoptionOrganizationId"] = intended.OrganizationId.ToString();
            config["Commerce:SourceRoot"] = root;
            using var rejected = await otherClient.PostAsJsonAsync(PathFor(other, otherBrand), new { slug = "synthetic-adoption" });
            rejected.StatusCode.Should().Be(HttpStatusCode.Forbidden, await rejected.Content.ReadAsStringAsync());
            (await otherClient.GetFromJsonAsync<JsonElement>(CommercePath(other, otherBrand))).GetProperty("data").ValueKind.Should().Be(JsonValueKind.Null);

            await fixture.SetRoleAsync(intended, "editor");
            using var editorRejected = await intendedClient.PostAsJsonAsync(PathFor(intended, intendedBrand), new { slug = "synthetic-adoption" });
            editorRejected.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            await fixture.SetRoleAsync(intended, "owner");
            using var adopted = await intendedClient.PostAsJsonAsync(PathFor(intended, intendedBrand), new { slug = "synthetic-adoption" });
            adopted.StatusCode.Should().Be(HttpStatusCode.OK, await adopted.Content.ReadAsStringAsync());
            var saved = (await intendedClient.GetFromJsonAsync<JsonElement>(CommercePath(intended, intendedBrand))).GetProperty("data");
            saved.GetProperty("revision").GetInt32().Should().Be(1);
            saved.GetProperty("snapshot").GetProperty("identity").GetProperty("name").GetString().Should().Be("Private synthetic donor");
            saved.GetProperty("snapshot").GetProperty("sources")[0].GetProperty("revision").GetInt32().Should().Be(7);
            using var repeated = await intendedClient.PostAsJsonAsync(PathFor(intended, intendedBrand), new { slug = "synthetic-adoption" });
            repeated.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private static async Task<Guid> CreateBrand(HttpClient client, OrganizationAccess owner)
    {
        using var response = await client.PostAsJsonAsync($"/api/organizations/{owner.OrganizationId}/brands", new { name = "Synthetic adoption fixture", timeZone = "Asia/Tashkent" });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("id").GetGuid();
    }
    private static string CommercePath(OrganizationAccess owner, Guid brand) => $"/api/organizations/{owner.OrganizationId}/brands/{brand}/commerce";
    private static string PathFor(OrganizationAccess owner, Guid brand) => CommercePath(owner, brand) + "/adopt-preserved";
    private static async Task CreateDonor(string root)
    {
        foreach (var domain in new[] { "brand", "catalogue" })
        {
            var directory = Path.Combine(root, $"ocharo-{domain}.backend-services", domain == "brand" ? "OcharoBrand.Api" : "OcharoCatalogue.Api", "App_Data");
            Directory.CreateDirectory(directory);
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, $"ocharo-{domain}.db"), Pooling = false }.ToString());
            await connection.OpenAsync();
            await using var schema = connection.CreateCommand();
            schema.CommandText = domain == "brand"
                ? "CREATE TABLE drafts(Id TEXT, Revision INTEGER, Json TEXT); CREATE TABLE logo_versions(Id TEXT, Json TEXT); CREATE TABLE media_blobs(Sha256 TEXT, ContentType TEXT, BlobPath TEXT);"
                : "CREATE TABLE products(Id TEXT, Revision INTEGER, Json TEXT, SortOrder INTEGER); CREATE TABLE collections(Id TEXT, Revision INTEGER, Json TEXT, SortOrder INTEGER); CREATE TABLE media_blobs(Sha256 TEXT, ContentType TEXT, BlobPath TEXT);";
            await schema.ExecuteNonQueryAsync();
            if (domain == "brand")
            {
                await using var insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO drafts VALUES ('private-donor',7,$json)";
                insert.Parameters.AddWithValue("$json", JsonSerializer.Serialize(new BrandDraft { Identity = new BrandIdentity { Name = "Private synthetic donor" } }, StoredJsonConstants.Default));
                await insert.ExecuteNonQueryAsync();
            }
        }
    }
}
