using AwesomeAssertions;
using BrandWorkspace.Api;
using BrandWorkspace.Api.Configurations;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Files;
using BrandWorkspace.Infrastructure.Preparation.Services;
using BrandWorkspace.Persistence.Preparation.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Uses real PostgreSQL, people the Identity token would name, and test-only organizations.</summary>
public sealed class PreparationDatabaseFixture : IAsyncLifetime
{
    private readonly PostgresFixture _database = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());
    public WebApiTestHost<ApiAssemblyMarker> Factory { get; private set; } = null!;
    public NpgsqlDataSource DataSource => Factory.Services.GetRequiredService<NpgsqlDataSource>();
    /// <summary>The database's full connection string, for another host over the same database.</summary>
    public string ConnectionString => _database.ConnectionString;
    public PreparationService Service { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        var migrate = await DeskTestHost.MigrateAsync(_database.ConnectionString);
        migrate.Exit.Should().Be(0, migrate.Error);
        Factory = ApiContractTests.CreateFactory(_database.ConnectionString);
        Service = new PreparationService(new PreparationRepository(DataSource, TimeProvider.System, new ManualPackageBuilder(), NoDeskFiles.Instance), new ImageValidationService(), NoDeskFiles.Instance);
    }

    /// <summary>Creates a person, a new organization they own, and the claims their sign-in carries.</summary>
    public async Task<OrganizationAccess> CreateOwnerAsync()
    {
        var subject = Guid.NewGuid().ToString("D");
        var organization = Guid.NewGuid();
        var email = $"preparation-{Guid.NewGuid():N}@example.test";
        await using var command = DataSource.CreateCommand("""
            INSERT INTO access.users(id,"DisplayName","Email","IsActive","SecurityStamp","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
            VALUES (@user,'Preparation fixture',@email,true,@stamp,false,false,false,false,0);
            INSERT INTO access.organizations(id,name,slug) VALUES (@organization,'Non-apparel fixture',@slug);
            INSERT INTO access.memberships(organization_id,user_id,role) VALUES (@organization,@user,'owner');
            """);
        command.Parameters.AddWithValue("user", subject);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("stamp", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("organization", organization);
        command.Parameters.AddWithValue("slug", $"fixture-{Guid.NewGuid():N}");
        await command.ExecuteNonQueryAsync();
        DeskTestHost.Register(new(subject, organization, "owner", "Preparation fixture", email));
        return new OrganizationAccess(organization, subject, "owner", true, true);
    }

    /// <summary>A client of the shared host that signs in as the person; it follows later <see cref="SetRoleAsync"/> changes.</summary>
    public Task<HttpClient> LoginAsync(OrganizationAccess access) => LoginAsync(Factory, access);

    /// <summary>A client of another host over the same database that signs in as the person.</summary>
    public Task<HttpClient> LoginAsync(WebApiTestHost<ApiAssemblyMarker> factory, OrganizationAccess access) =>
        Task.FromResult(DeskTestHost.CreateClient(factory, access.UserId));

    /// <summary>Changes the person's role in the claims and the membership row; null removes the membership.</summary>
    public async Task SetRoleAsync(OrganizationAccess access, string? role)
    {
        var person = DeskTestHost.Find(access.UserId)!;
        DeskTestHost.Register(person with { Role = role });
        await using var command = DataSource.CreateCommand(role is null
            ? "DELETE FROM access.memberships WHERE organization_id=$1 AND user_id=$2"
            : """
              INSERT INTO access.memberships(organization_id,user_id,role) VALUES ($1,$2,$3)
              ON CONFLICT (organization_id,user_id) DO UPDATE SET role=EXCLUDED.role
              """);
        command.Parameters.AddWithValue(access.OrganizationId);
        command.Parameters.AddWithValue(access.UserId);
        if (role is not null) command.Parameters.AddWithValue(role);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}
