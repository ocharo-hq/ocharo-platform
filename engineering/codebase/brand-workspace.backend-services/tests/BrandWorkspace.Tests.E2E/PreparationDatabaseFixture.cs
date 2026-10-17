using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Api;
using BrandWorkspace.Api.Configurations;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Infrastructure.Preparation.Services;
using BrandWorkspace.Persistence.Access;
using BrandWorkspace.Persistence.Preparation.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Testing;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Uses real PostgreSQL, real Identity users, and test-only organizations.</summary>
public sealed class PreparationDatabaseFixture : IAsyncLifetime
{
    private readonly PostgresFixture _database = new(new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build());
    public WebApiTestHost<ApiAssemblyMarker> Factory { get; private set; } = null!;
    public NpgsqlDataSource DataSource => Factory.Services.GetRequiredService<NpgsqlDataSource>();
    public PreparationService Service { get; private set; } = null!;
    private const string Password = "Preparation_Fixture_42!";

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = _database.ConnectionString });
        builder.Configure(migrate: true);
        var app = builder.Build();
        app.Configure();
        await app.RunCommandOrHostAsync(["--migrate"]);
        Factory = ApiContractTests.CreateFactory(_database.ConnectionString);
        Service = new PreparationService(new PreparationRepository(DataSource, TimeProvider.System, new ManualPackageBuilder()), new ImageValidationService());
    }

    public async Task<OrganizationAccess> CreateOwnerAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccessUser>>();
        var email = $"preparation-{Guid.NewGuid():N}@example.test";
        var user = new AccessUser { UserName = email, Email = email, DisplayName = "Preparation fixture" };
        var result = await users.CreateAsync(user, Password);
        result.Succeeded.Should().BeTrue(string.Join("; ", result.Errors.Select(error => error.Description)));
        var repository = scope.ServiceProvider.GetRequiredService<IAccessRepository>();
        var organization = await repository.CreateOrganizationAsync("Non-apparel fixture", $"fixture-{Guid.NewGuid():N}", user.Id, CancellationToken.None);
        return (await repository.FindMembershipAsync(organization!.Id, user.Id, CancellationToken.None))!;
    }

    public async Task<HttpClient> LoginAsync(OrganizationAccess access)
    {
        using var scope = Factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<AccessUser>>().FindByIdAsync(access.UserId);
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var session = await client.GetFromJsonAsync<JsonElement>("/api/auth/session");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.GetProperty("data").GetProperty("csrfToken").GetString());
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = user!.Email, password = Password });
        login.IsSuccessStatusCode.Should().BeTrue(await login.Content.ReadAsStringAsync());
        session = await client.GetFromJsonAsync<JsonElement>("/api/auth/session");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.GetProperty("data").GetProperty("csrfToken").GetString());
        return client;
    }

    public async Task SetRoleAsync(OrganizationAccess access, string? role)
    {
        await using var command = DataSource.CreateCommand(role is null
            ? "DELETE FROM access.memberships WHERE organization_id=$1 AND user_id=$2"
            : "UPDATE access.memberships SET role=$3 WHERE organization_id=$1 AND user_id=$2");
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
