using AwesomeAssertions;
using BrandWorkspace.Api;
using BrandWorkspace.Api.Configurations;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Repositories;
using BrandWorkspace.Application.Files.Constants;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Files.Services;
using BrandWorkspace.Application.Files.Repositories;
using BrandWorkspace.Infrastructure.Files.Services;
using BrandWorkspace.Infrastructure.Preparation.Services;
using BrandWorkspace.Persistence.Preparation.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
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
    /// <summary>The shared host's connection factory, for repositories built outside its container.</summary>
    public IDbConnectionFactory Connections => Factory.Services.GetRequiredService<IDbConnectionFactory>();
    /// <summary>The database's full connection string, for another host over the same database.</summary>
    public string ConnectionString => _database.ConnectionString;
    /// <summary>The preparation workflow over the shared database, a failed result thrown as its exception.</summary>
    public PreparationServiceBridge Service { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        var migrate = await DeskTestHost.MigrateAsync(_database.ConnectionString);
        migrate.Exit.Should().Be(0, migrate.Error);
        Factory = ApiContractTests.CreateFactory(_database.ConnectionString);
        var repository = new PreparationRepository(
            Connections,
            TimeProvider.System,
            new ManualPackageBuilder(),
            NoDeskFiles.Instance);
        Service = new PreparationServiceBridge(
            new PreparationService(repository, new ImageValidationService(), NoDeskFiles.Instance));
    }

    /// <summary>Creates a person, a new organization they own, and the claims their sign-in carries.</summary>
    public async Task<OrganizationAccess> CreateOwnerAsync()
    {
        var subject = Guid.NewGuid().ToString("D");
        var organization = Guid.NewGuid();
        var email = $"preparation-{Guid.NewGuid():N}@example.test";
        await using var command = DataSource.CreateCommand("""
            INSERT INTO access.users(id,"DisplayName","Email","IsActive","SecurityStamp","EmailConfirmed",
                "PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
            VALUES (@user,'Preparation fixture',@email,true,@stamp,false,false,false,false,0);
            INSERT INTO access.organizations(id,name,slug) VALUES (@organization,'Non-apparel fixture',@slug);
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

    /// <summary>
    /// A client of the shared host that signs in as the person; it follows later <see cref="SetRoleAsync"/> changes.
    /// </summary>
    public Task<HttpClient> LoginAsync(OrganizationAccess access) => LoginAsync(Factory, access);

    /// <summary>A client of another host over the same database that signs in as the person.</summary>
    public Task<HttpClient> LoginAsync(WebApiTestHost<ApiAssemblyMarker> factory, OrganizationAccess access) =>
        Task.FromResult(DeskTestHost.CreateClient(factory, access.UserId));

    /// <summary>
    /// Changes the person's role in the claims their sign-in carries, where Identity keeps it; null removes the
    /// organization from them.
    /// </summary>
    public Task SetRoleAsync(OrganizationAccess access, string? role)
    {
        var person = DeskTestHost.Find(access.UserId)!;
        DeskTestHost.Register(person with { Role = role });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        await _database.DisposeAsync();
    }
}
