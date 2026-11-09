using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api.Configurations;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using Microsoft.Extensions.Configuration;
using WoW.Two.Sdk.Beta.Be.Meta;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>
/// The <c>import events</c> and <c>import sourcing</c> verbs, run as the image runs them, resolve <c>--actor</c> to a
/// person Desk projected from Identity, by Identity user id or by email, and refuse anyone else.
/// </summary>
public sealed class ImportVerbTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    [Fact]
    public async Task ImportEvents_ShouldAuthorAsThePersonTheActorNames_ByIdentityUserIdOrEmail()
    {
        var owner = await fixture.CreateOwnerAsync();
        var directory = Directory.CreateTempSubdirectory("events-verb-");
        try
        {
            var bundle = Path.Combine(directory.FullName, "events.json");
            await File.WriteAllTextAsync(bundle, JsonSerializer.Serialize(new
            {
                format = EventImportService.Format,
                source = "verb fixture",
                events = new[] { new { key = "verb-fair", title = "Verb fair", scope = "local", format = "market" } },
            }));

            var email = (await EmailAsync(owner)).ToUpperInvariant();
            var byId = await RunAsync("events", bundle, owner.OrganizationId, owner.UserId);
            var byEmail = await RunAsync("events", bundle, owner.OrganizationId, email, "--dry-run");

            byId.Exit.Should().Be(0, byId.Error);
            Report(byId).GetProperty("created").GetInt32().Should().Be(1);
            (await AuthorAsync("SELECT created_by FROM events.showcase_events WHERE organization_id=$1", owner))
                .Should().Be(owner.UserId);
            byEmail.Exit.Should().Be(0, byEmail.Error);
            Report(byEmail).GetProperty("dryRun").GetBoolean().Should().BeTrue();
            Report(byEmail).GetProperty("existing").GetInt32().Should().Be(1);
        }
        finally { directory.Delete(true); }
    }

    [Fact]
    public async Task ImportSourcing_ShouldAuthorAsThePersonTheActorNames()
    {
        var owner = await fixture.CreateOwnerAsync();
        var directory = Directory.CreateTempSubdirectory("sourcing-verb-");
        try
        {
            var bundle = Path.Combine(directory.FullName, "sourcing.json");
            await File.WriteAllTextAsync(bundle, JsonSerializer.Serialize(new
            {
                format = SourcingImportService.Format,
                source = "verb fixture",
                suppliers = new[]
                {
                    new
                    {
                        key = "verb-supplier", name = "Verb Supplier", stage = "contacted", country = "IN",
                        routes = new[]
                        {
                            new
                            {
                                channel = "form", destination = "https://example.test/form", verified = false,
                                isPrimary = true,
                            },
                        },
                    },
                },
                media = Array.Empty<object>(),
            }));

            var run = await RunAsync("sourcing", bundle, owner.OrganizationId, await EmailAsync(owner));

            run.Exit.Should().Be(0, run.Error);
            Report(run).GetProperty("suppliers").GetProperty("created").GetInt32().Should().Be(1);
            (await AuthorAsync("SELECT created_by FROM sourcing.suppliers WHERE organization_id=$1", owner))
                .Should().Be(owner.UserId);
        }
        finally { directory.Delete(true); }
    }

    [Theory]
    [InlineData("events")]
    [InlineData("sourcing")]
    public async Task Import_ShouldRefuse_WhenDeskNeverSawTheActorOrTheOrganization(string verb)
    {
        var owner = await fixture.CreateOwnerAsync();
        var bundle = Path.Combine(Path.GetTempPath(), $"never-read-{Guid.NewGuid():N}.json");

        var unknownActor = () => RunAsync(verb, bundle, owner.OrganizationId, "nobody@example.test");
        var unknownOrganization = () => RunAsync(verb, bundle, Guid.NewGuid(), owner.UserId);

        (await unknownActor.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("The actor is not a provisioned operator.");
        (await unknownOrganization.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Desk knows no such organization.");
    }

    /// <summary>Runs <c>import {verb}</c> through <see cref="ServiceHost"/> over the fixture's database.</summary>
    private async Task<(int Exit, string Output, string Error)> RunAsync(
        string verb,
        string bundle,
        Guid organization,
        string actor,
        params string[] flags)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args =
        [
            "import", verb, .. flags, "--bundle", bundle, "--organization", organization.ToString("D"), "--actor", actor,
        ];
        var exit = await ServiceHost.RunAsync(args, builder =>
        {
            builder.Configuration.AddInMemoryCollection(
                new Dictionary<string, string?>(DeskTestHost.IdentityClientSettings)
                {
                    ["ConnectionStrings:Database"] = fixture.ConnectionString,
                });
            builder.Configure();
        }, _ => Task.CompletedTask, verbs => verbs.AddDeskVerbs(), output, error, CancellationToken.None);
        return (exit, output.ToString(), error.ToString());
    }

    private static JsonElement Report((int Exit, string Output, string Error) run) =>
        JsonDocument.Parse(run.Output).RootElement;

    private async Task<string> EmailAsync(OrganizationAccess access) =>
        (string)(await ScalarAsync("""SELECT "Email" FROM access.users WHERE id=$1""", access.UserId))!;

    private async Task<string?> AuthorAsync(string sql, OrganizationAccess access) =>
        (string?)await ScalarAsync(sql, access.OrganizationId);

    private async Task<object?> ScalarAsync(string sql, object value)
    {
        await using var command = fixture.DataSource.CreateCommand(sql);
        command.Parameters.AddWithValue(value);
        return await command.ExecuteScalarAsync();
    }
}
