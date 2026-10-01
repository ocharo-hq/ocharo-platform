using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Npgsql;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using BrandWorkspace.Persistence.DesignDocuments.Repositories;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises real PostgreSQL persistence, authenticated tenant boundaries, and revision races.</summary>
public sealed class DesignDocumentTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [Fact]
    public async Task Readiness_ShouldRequireTheDocumentSchema()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage ready = await client.GetAsync("/health/ready");
        ready.StatusCode.Should().Be(HttpStatusCode.OK);
        await using NpgsqlCommand hide = fixture.DataSource.CreateCommand(
            "ALTER SCHEMA design_documents RENAME TO design_documents_unavailable");
        await hide.ExecuteNonQueryAsync();
        try
        {
            using HttpResponseMessage missing = await client.GetAsync("/health/ready");
            missing.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            await using NpgsqlCommand restore = fixture.DataSource.CreateCommand(
                "ALTER SCHEMA design_documents_unavailable RENAME TO design_documents");
            await restore.ExecuteNonQueryAsync();
        }
        using HttpResponseMessage recovered = await client.GetAsync("/health/ready");
        recovered.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("catalogue")]
    [InlineData("business-card")]
    public async Task Workflow_ShouldPersistRevisions_AndArchiveWithoutLosingContent(string kind)
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);
        JsonElement snapshot = Snapshot("Original text");
        using HttpResponseMessage created = await client.PostAsJsonAsync(path, new { kind, title = "First", snapshot });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        created.Headers.CacheControl!.NoStore.Should().BeTrue();
        JsonElement first = await DataAsync(created);
        Guid id = first.GetProperty("id").GetGuid();
        first.GetProperty("revision").GetInt32().Should().Be(1);
        first.GetProperty("organizationId").GetGuid().Should().Be(owner.OrganizationId);

        using HttpResponseMessage saved = await client.PutAsJsonAsync($"{path}/{id}", new
        {
            title = "Second", snapshot = Snapshot("O‘zbekcha\nNext line"), expectedRevision = 1
        });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        (await DataAsync(saved)).GetProperty("revision").GetInt32().Should().Be(2);
        // A new repository instance reconstructs its result from PostgreSQL rather than a service cache.
        DesignDocument reloaded = await Service().GetDocumentAsync(owner, id, Token);
        reloaded.Snapshot.GetProperty("text").GetString().Should().Be("O‘zbekcha\nNext line");
        DesignDocument previous = await Service().GetRevisionAsync(owner, id, 1, Token);
        previous.Title.Should().Be("First");
        previous.Snapshot.GetProperty("text").GetString().Should().Be("Original text");
        previous.Archived.Should().BeFalse();

        using HttpResponseMessage archived = await client.PostAsJsonAsync($"{path}/{id}/archive", new { expectedRevision = 2 });
        archived.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement archivedDocument = await DataAsync(archived);
        archivedDocument.GetProperty("archived").GetBoolean().Should().BeTrue();
        archivedDocument.GetProperty("revision").GetInt32().Should().Be(3);
        using HttpResponseMessage hidden = await client.GetAsync(path);
        (await DataAsync(hidden)).GetArrayLength().Should().Be(0);
        using HttpResponseMessage included = await client.GetAsync($"{path}?kind={kind}&includeArchived=true");
        (await DataAsync(included)).GetArrayLength().Should().Be(1);
        using HttpResponseMessage history = await client.GetAsync($"{path}/{id}/revisions");
        (await DataAsync(history)).EnumerateArray().Select(item => item.GetProperty("revision").GetInt32())
            .Should().Equal(3, 2, 1);
        using HttpResponseMessage historical = await client.GetAsync($"{path}/{id}/revisions/1");
        (await DataAsync(historical)).GetProperty("snapshot").GetProperty("text").GetString().Should().Be("Original text");
        using HttpResponseMessage editArchived = await client.PutAsJsonAsync($"{path}/{id}", new
        {
            title = "Should fail", snapshot, expectedRevision = 3
        });
        editArchived.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TenantScope_ShouldRejectForeignDocumentsHistoryWritesAndAssociations()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        OrganizationAccess other = await fixture.CreateOwnerAsync();
        DesignDocument document = await Service().CreateDocumentAsync(owner, "catalogue", Input("Own"), Token);
        using HttpClient client = await fixture.LoginAsync(other);
        string ownPath = Path(other);
        string foreignIdPath = $"{ownPath}/{document.Id}";
        foreach (string path in new[]
        {
            $"{Path(owner)}/{document.Id}", foreignIdPath, $"{foreignIdPath}/revisions", $"{foreignIdPath}/revisions/1"
        })
        {
            using HttpResponseMessage response = await client.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        using HttpResponseMessage save = await client.PutAsJsonAsync(foreignIdPath, new
        {
            title = "Foreign overwrite", snapshot = Snapshot("Foreign"), expectedRevision = 1
        });
        save.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using HttpResponseMessage archive = await client.PostAsJsonAsync(
            $"{foreignIdPath}/archive", new { expectedRevision = 1 });
        archive.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using HttpResponseMessage list = await client.GetAsync(ownPath);
        (await DataAsync(list)).GetArrayLength().Should().Be(0);

        await using NpgsqlCommand association = fixture.DataSource.CreateCommand(
            """
            INSERT INTO design_documents.revisions(
                organization_id,document_id,revision,title,snapshot,archived,saved_by,saved_at)
            VALUES($1,$2,2,'Foreign','{}',false,$3,now())
            """);
        association.Parameters.AddWithValue(other.OrganizationId);
        association.Parameters.AddWithValue(document.Id);
        association.Parameters.AddWithValue(other.UserId);
        Func<Task> invalidAssociation = async () => await association.ExecuteNonQueryAsync();
        (await invalidAssociation.Should().ThrowAsync<PostgresException>()).Which.SqlState
            .Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        (await Service().GetDocumentAsync(owner, document.Id, Token)).Revision.Should().Be(1);
    }

    [Fact]
    public async Task Roles_ShouldRecheckMembershipAndAntiforgeryForEveryRequest()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        DesignDocument document = await Service().CreateDocumentAsync(owner, "business-card", Input("Owner"), Token);
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = $"{Path(owner)}/{document.Id}";
        await fixture.SetRoleAsync(owner, "viewer");
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync(path, new
        {
            title = "Denied", snapshot = Snapshot("Denied"), expectedRevision = 1
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync($"{path}/archive", new { expectedRevision = 1 })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        await fixture.SetRoleAsync(owner, "editor");
        (await client.PutAsJsonAsync(path, new
        {
            title = "Editor", snapshot = Snapshot("Allowed"), expectedRevision = 1
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        (await client.PostAsJsonAsync($"{path}/archive", new { expectedRevision = 2 })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        await fixture.SetRoleAsync(owner, null);
        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConcurrentSaves_ShouldKeepOneWinner_AndProtectImmutableHistory()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        DesignDocumentService service = Service();
        DesignDocument original = await service.CreateDocumentAsync(owner, "catalogue", Input("Original"), Token);
        Task<(DesignDocument? Value, PreparationException? Error)> first = CaptureAsync(() =>
            service.SaveDocumentAsync(owner, original.Id, 1, Input("First"), Token));
        Task<(DesignDocument? Value, PreparationException? Error)> second = CaptureAsync(() =>
            service.SaveDocumentAsync(owner, original.Id, 1, Input("Second"), Token));
        (DesignDocument? Value, PreparationException? Error)[] outcomes = await Task.WhenAll(first, second);
        outcomes.Count(item => item.Error is null).Should().Be(1);
        outcomes.Single(item => item.Error is not null).Error!.Code.Should().Be("conflict");
        DesignDocument current = await service.GetDocumentAsync(owner, original.Id, Token);
        current.Revision.Should().Be(2);
        (await service.GetRevisionAsync(owner, original.Id, 1, Token)).Title.Should().Be("Original");
        (await service.GetRevisionsAsync(owner, original.Id, 50, 0, Token)).Should().HaveCount(2);
        foreach (string sql in new[]
        {
            "UPDATE design_documents.revisions SET title='Mutated' WHERE organization_id=$1 AND document_id=$2",
            "DELETE FROM design_documents.revisions WHERE organization_id=$1 AND document_id=$2"
        })
        {
            await using NpgsqlCommand mutate = fixture.DataSource.CreateCommand(sql);
            mutate.Parameters.AddWithValue(owner.OrganizationId);
            mutate.Parameters.AddWithValue(original.Id);
            Func<Task> operation = async () => await mutate.ExecuteNonQueryAsync();
            (await operation.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("55000");
        }
        using HttpClient client = await fixture.LoginAsync(owner);
        using HttpResponseMessage conflict = await client.PutAsJsonAsync($"{Path(owner)}/{original.Id}", new
        {
            title = "Stale", snapshot = Snapshot("Stale"), expectedRevision = 1
        });
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await conflict.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("conflict");
        (await service.GetDocumentAsync(owner, original.Id, Token)).Title.Should().Be(current.Title);
    }

    [Fact]
    public async Task InvalidRequests_ShouldReturnBoundedProblems_WithoutCreatingRows()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);
        string[] invalid =
        [
            "null",
            "{",
            "{\"\\uD800\":1}",
            "{\"kind\":\"other\",\"title\":\"Test\",\"snapshot\":{}}",
            "{\"kind\":\"catalogue\",\"title\":\"Test\"}",
            "{\"kind\":\"catalogue\",\"title\":\"Test\",\"snapshot\":[]}",
            "{\"kind\":\"catalogue\",\"title\":\"One\",\"title\":\"Two\",\"snapshot\":{}}",
            "{\"kind\":\"catalogue\",\"title\":\"Test\",\"snapshot\":{\"a\":1,\"a\":2}}",
            "{\"kind\":\"catalogue\",\"title\":\"Test\",\"snapshot\":{},\"organizationId\":\"forged\"}"
        ];
        foreach (string json in invalid)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await client.PostAsync(path, content);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, json);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
        }
        string oversized = "{\"kind\":\"catalogue\",\"title\":\"Test\",\"snapshot\":{\"text\":\""
            + new string('x', DesignDocumentValidationService.MaximumSnapshotBytes + 8192) + "\"}}";
        using var large = new StringContent(oversized, Encoding.UTF8, "application/json");
        using HttpResponseMessage tooLarge = await client.PostAsync(path, large);
        tooLarge.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await Service().GetDocumentsAsync(owner, null, true, 50, 0, Token)).Should().BeEmpty();
    }

    private DesignDocumentService Service()
    {
        return new DesignDocumentService(
            new DesignDocumentRepository(fixture.DataSource, TimeProvider.System),
            new DesignDocumentValidationService(), new BrandWorkspace.Persistence.Commerce.CommerceRepository(fixture.DataSource, TimeProvider.System));
    }

    private static string Path(OrganizationAccess owner)
    {
        return $"/api/organizations/{owner.OrganizationId}/design-documents";
    }

    private static DesignDocumentInput Input(string text)
    {
        return new DesignDocumentInput(text, Snapshot(text));
    }

    private static JsonElement Snapshot(string text)
    {
        return JsonSerializer.SerializeToElement(new { schemaVersion = 1, text, pages = new[] { new { width = 210 } } });
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private static async Task<(DesignDocument? Value, PreparationException? Error)> CaptureAsync(
        Func<Task<DesignDocument>> action)
    {
        try { return (await action(), null); }
        catch (PreparationException exception) { return (null, exception); }
    }
}
