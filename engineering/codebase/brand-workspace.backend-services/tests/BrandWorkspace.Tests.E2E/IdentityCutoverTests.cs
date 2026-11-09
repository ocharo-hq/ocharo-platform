using System.Data.Common;
using System.Net;
using AwesomeAssertions;
using BrandWorkspace.Persistence.Foundation.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>
/// Migration <c>013-identity-cutover</c> drops Desk's own sign-in tables, on a fresh database and on one that still
/// holds their rows, and keeps the projection of Identity's people and organizations.
/// </summary>
public sealed class IdentityCutoverTests
{
    /// <summary>Holds every table 013 drops.</summary>
    private static readonly string[] Retired =
    [
        "access.sessions", "access.memberships", "access.sso_codes", "access.sso_grants", "access.telegram_links",
        "access.telegram_link_requests", "access.telegram_codes", "access.identity_roles", "access.identity_role_claims",
        "access.identity_user_claims", "access.identity_user_logins", "access.identity_user_roles",
        "access.identity_user_tokens",
    ];

    [Fact]
    public async Task FreshDatabase_ShouldMigrateThrough013WithoutTheRetiredTables()
    {
        await using var database = new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());
        await database.StartAsync();

        var migrate = await DeskTestHost.MigrateAsync(database.ConnectionString);

        migrate.Exit.Should().Be(0, migrate.Error);
        await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString);
        (await HistoryAsync(dataSource, 13)).Should().Be(("identity-cutover", "contract", "v0.2.7"));
        (await ExistingAsync(dataSource, Retired)).Should().BeEmpty();
        (await ExistingAsync(dataSource, ["access.users", "access.organizations"])).Should().HaveCount(2);
        await using var factory = ApiContractTests.CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK, "readiness reads no retired table");
    }

    [Fact]
    public async Task DatabaseHoldingRetiredRows_ShouldMigrateThrough013AsADeployedRun()
    {
        await using var database = new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());
        await database.StartAsync();
        var source = Directory.CreateTempSubdirectory("brand-workspace-v0.2.3-set-");
        try
        {
            // The database a deployed v0.2.3 left: 001-012 applied, with rows in every table 013 drops.
            WriteEmbeddedSet(source.FullName, ordinal => ordinal <= 12);
            (await ApplyAsync(database.ConnectionString, source.FullName)).Should().BeNull();
            await using var dataSource = NpgsqlDataSource.Create(database.ConnectionString);
            var (person, organization) = await SeedRetiredRowsAsync(dataSource);

            // The 011 reserved for this cutover would never apply there: the deployed migrator refuses an ordinal
            // below the applied level, so the cutover takes the next free one.
            var reserved = Path.Combine(source.FullName, "011-identity-cutover");
            WriteEmbeddedSet(source.FullName, ordinal => ordinal == 13);
            Directory.Move(Path.Combine(source.FullName, "013-identity-cutover"), reserved);
            (await ApplyAsync(database.ConnectionString, source.FullName))
                .Should().Contain("below the applied level 012");

            var migrate = await DeskTestHost.MigrateAsync(database.ConnectionString, deployed: true);

            migrate.Exit.Should().Be(0, migrate.Error);
            (await HistoryAsync(dataSource, 13)).Should().Be(("identity-cutover", "contract", "v0.2.7"));
            (await ExistingAsync(dataSource, Retired)).Should().BeEmpty();
            (await ScalarAsync(dataSource, "SELECT \"DisplayName\" FROM access.users WHERE id=$1", person))
                .Should().Be("Cutover fixture", "the projection keeps every person");
            (await ScalarAsync(
                    dataSource,
                    "SELECT created_by FROM events.showcase_events WHERE organization_id=$1",
                    organization))
                .Should().Be(person, "authored records keep their authors");
        }
        finally { source.Delete(true); }
    }

    /// <summary>Writes the migrations Desk embeds whose ordinal passes <paramref name="include"/> as an on-disk set.</summary>
    private static void WriteEmbeddedSet(string root, Func<int, bool> include)
    {
        var assembly = typeof(FoundationRepository).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(name => name.StartsWith("Migrations/")))
        {
            var parts = name.Split('/');
            if (!include(int.Parse(parts[1][..3]))) continue;
            Directory.CreateDirectory(Path.Combine(root, parts[1]));
            using var resource = assembly.GetManifestResourceStream(name)!;
            using var file = File.Create(Path.Combine(root, parts[1], parts[2]));
            resource.CopyTo(file);
        }
    }

    /// <summary>
    /// Applies an on-disk set as Desk's default set, the way a deployed v0.2.3 did; returns null once applied, else
    /// the refusal.
    /// </summary>
    private static async Task<string?> ApplyAsync(string connection, string root)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NpgsqlDataSource.Create(connection));
        services.AddSingleton<DbDataSource>(provider => provider.GetRequiredService<NpgsqlDataSource>());
        services.AddDataSourceConnectionFactory();
        services.AddDatabaseBespokeMigrations(root, options =>
        {
            options.Version = "0.2.3";
            options.Release = "0.2.3";
            options.AdvisoryLockId = 8_264_001;
            options.LocalRun = false;
        });
        await using var provider = services.BuildServiceProvider();
        var applied = await provider.GetRequiredService<IMigrationRunnerService>().ApplyAsync("v0.2.3");
        return applied.IsFailure(out AppError? error, out _) ? error.Message : null;
    }

    /// <summary>Stores a person, an organization, an event they authored and a row in every retired table.</summary>
    private static async Task<(string Person, Guid Organization)> SeedRetiredRowsAsync(NpgsqlDataSource dataSource)
    {
        var person = Guid.NewGuid().ToString("D");
        var organization = Guid.NewGuid();
        var session = Guid.NewGuid();
        await using var command = dataSource.CreateCommand("""
            INSERT INTO access.users(id,"DisplayName","Email","NormalizedEmail","PasswordHash","SecurityStamp","IsActive",
                "EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
            VALUES (@person,'Cutover fixture','cutover@example.test','CUTOVER@EXAMPLE.TEST','hash','stamp',true,
                true,false,false,true,0);
            INSERT INTO access.organizations(id,name,slug) VALUES (@organization,'Cutover fixture','cutover-fixture');
            INSERT INTO access.memberships(organization_id,user_id,role) VALUES (@organization,@person,'owner');
            INSERT INTO access.sessions(id,user_id,security_stamp,expires_at)
                VALUES (@session,@person,'stamp',now() + interval '8 hours');
            INSERT INTO access.telegram_links(user_id,telegram_id,phone,linked_at)
                VALUES (@person,4242,'+998901234567',now());
            INSERT INTO access.telegram_link_requests(token_hash,user_id,session_id,expires_at)
                VALUES ('link-token',@person,@session,now() + interval '5 minutes');
            INSERT INTO access.telegram_codes(id,subject,scope,protected_code,created_at,expires_at)
                VALUES (gen_random_uuid(),'+998901234567','sign-in','protected',now(),now() + interval '5 minutes');
            INSERT INTO access.sso_codes(code_hash,client_id,redirect_uri,challenge,user_id,session_id,organization_id,
                expires_at,session_expires_at)
                VALUES ('code','studio','https://studio.example.test/callback','challenge',@person,@session,
                    @organization,now() + interval '1 minute',now() + interval '8 hours');
            INSERT INTO access.sso_grants(token_hash,client_id,user_id,session_id,organization_id,expires_at)
                VALUES ('grant','studio',@person,@session,@organization,now() + interval '1 hour');
            INSERT INTO access.identity_roles("Id","Name","NormalizedName") VALUES ('role','Operator','OPERATOR');
            INSERT INTO access.identity_role_claims("RoleId","ClaimType","ClaimValue") VALUES ('role','scope','desk');
            INSERT INTO access.identity_user_claims("UserId","ClaimType","ClaimValue") VALUES (@person,'scope','desk');
            INSERT INTO access.identity_user_logins("LoginProvider","ProviderKey","UserId")
                VALUES ('telegram','4242',@person);
            INSERT INTO access.identity_user_roles("UserId","RoleId") VALUES (@person,'role');
            INSERT INTO access.identity_user_tokens("UserId","LoginProvider","Name","Value")
                VALUES (@person,'telegram','refresh','token');
            INSERT INTO events.showcase_events(organization_id,id,title,scope,format,date_precision,participation,status,
                priority,notes,revision,created_by,created_at,updated_at)
                VALUES (@organization,gen_random_uuid(),'Cutover fair','local','market','unknown','','candidate',
                    'normal','',1,@person,now(),now());
            """);
        command.Parameters.AddWithValue("person", person);
        command.Parameters.AddWithValue("organization", organization);
        command.Parameters.AddWithValue("session", session);
        await command.ExecuteNonQueryAsync();
        foreach (var table in Retired)
            ((long)(await ScalarAsync(dataSource, $"SELECT count(*) FROM {table}"))!).Should().Be(1, table);
        return (person, organization);
    }

    /// <summary>Reads a migration's name, kind and <c>since</c> from Desk's history.</summary>
    private static async Task<(string, string?, string?)> HistoryAsync(NpgsqlDataSource dataSource, int ordinal)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT name, kind, contract_since FROM public.migration_history WHERE ordinal=$1");
        command.Parameters.AddWithValue(ordinal);
        await using var reader = await command.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue($"{ordinal:D3} is applied");
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    /// <summary>Returns which of <paramref name="tables"/> exist.</summary>
    private static async Task<IReadOnlyList<string>> ExistingAsync(NpgsqlDataSource dataSource, string[] tables)
    {
        var existing = new List<string>();
        foreach (var table in tables)
            if (await ScalarAsync(dataSource, $"SELECT to_regclass('{table}')::text") is string) existing.Add(table);
        return existing;
    }

    private static async Task<object?> ScalarAsync(NpgsqlDataSource dataSource, string sql, params object[] values)
    {
        await using var command = dataSource.CreateCommand(sql);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        var scalar = await command.ExecuteScalarAsync();
        return scalar is DBNull ? null : scalar;
    }
}
