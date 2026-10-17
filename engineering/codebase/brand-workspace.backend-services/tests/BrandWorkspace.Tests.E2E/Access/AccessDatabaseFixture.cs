using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BrandWorkspace.Api;
using BrandWorkspace.Api.Configurations;
using BrandWorkspace.Persistence.Access;
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

namespace BrandWorkspace.Tests.E2E.Access;

public sealed class AccessDatabaseFixture : IAsyncLifetime
{
    private readonly PostgresFixture database = new(new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build());
    public string KeysPath { get; } = Path.Combine(Path.GetTempPath(), "brand-workspace-access-tests-" + Guid.NewGuid());
    public string Connection => database.ConnectionString;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:Database"] = Connection, ["DataProtection:KeysPath"] = KeysPath });
        builder.Configure(migrate: true);
        var app = builder.Build();
        app.Configure();
        await app.RunCommandOrHostAsync(["--migrate"]);
    }

    public WebApiTestHost<ApiAssemblyMarker> CreateFactory(string discriminator = "brand-workspace:access-tests")
    {
        var factory = new WebApiTestHost<ApiAssemblyMarker>
        {
            ConfigureHostHook = builder => builder.UseEnvironment(Environments.Development),
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = Connection,
                ["AllowedHosts"] = "localhost;127.0.0.1",
                ["DataProtection:KeysPath"] = KeysPath,
                ["DataProtection:ApplicationName"] = discriminator
            })
        };
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        return factory;
    }

    public static HttpClient CreateClient(WebApiTestHost<ApiAssemblyMarker> factory, bool cookies = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = cookies });

    public static async Task<AccessUser> CreateOperatorAsync(WebApiTestHost<ApiAssemblyMarker> factory, string password = "Test-only-Correct#927")
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AccessUser>>();
        var email = $"{Guid.NewGuid():N}@example.test";
        var user = new AccessUser { Email = email, UserName = email, DisplayName = "Test operator", EmailConfirmed = true, LockoutEnabled = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(",", result.Errors.Select(error => error.Code)));
        return user;
    }

    public static async Task<JsonElement> SessionAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/session");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body, string? csrf)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (csrf is not null) request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }

    public static async Task<string> LoginAsync(HttpClient client, AccessUser user, string password = "Test-only-Correct#927")
    {
        var session = await SessionAsync(client);
        var response = await PostAsync(client, "/api/auth/login", new { email = user.Email, password }, session.GetProperty("csrfToken").GetString());
        response.EnsureSuccessStatusCode();
        return (await SessionAsync(client)).GetProperty("csrfToken").GetString()!;
    }

    public async Task ExecuteAsync(string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await database.DisposeAsync();
        if (Directory.Exists(KeysPath)) Directory.Delete(KeysPath, true);
    }
}
