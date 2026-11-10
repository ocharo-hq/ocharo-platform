using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api.Configurations;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using Microsoft.Extensions.Configuration;
using WoW.Two.Sdk.Beta.Be.Meta;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>
/// The <c>import sourcing</c> verb, run as the image runs it, resolves <c>--actor</c> to a
/// person Desk projected from Identity, by Identity user id or by email, and refuses anyone else; Desk's events live
/// in WoW2 Events, so <c>import events</c> is gone (v0.3 Iteration 5).
/// </summary>
public sealed class ImportVerbTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
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
