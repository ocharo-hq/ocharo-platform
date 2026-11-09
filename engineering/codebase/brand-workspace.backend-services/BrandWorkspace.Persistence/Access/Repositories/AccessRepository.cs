using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Repositories;
using Dapper;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;

namespace BrandWorkspace.Persistence.Access.Repositories;

/// <summary>Accesses Desk's organizations, memberships and its projection of signed-in people.</summary>
public sealed class AccessRepository(IDbConnectionFactory connectionFactory) : IAccessRepository
{
    private readonly ConcurrentDictionary<string, string> projected = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, string> projectedOrganizations = new();

    public async Task<OrganizationAccess?> FindMembershipAsync(
        Guid organizationId,
        string userId,
        CancellationToken token)
    {
        const string sql =
            "SELECT role FROM access.memberships WHERE organization_id=@organizationId AND user_id=@userId";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        string? role = await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(sql, new { organizationId, userId }, cancellationToken: token));
        return role is null
            ? null
            : new OrganizationAccess(organizationId, userId, role, role is "owner" or "editor", role == "owner");
    }

    public async Task<Organization?> FindOrganizationAsync(Guid organizationId, string role, CancellationToken token)
    {
        const string sql = "SELECT name,slug FROM access.organizations WHERE id=@organizationId";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        OrganizationRow? row = await connection.QueryFirstOrDefaultAsync<OrganizationRow>(
            new CommandDefinition(sql, new { organizationId }, cancellationToken: token));
        return row is null ? null : new Organization(organizationId, row.Name, row.Slug, role);
    }

    public async Task ProjectUserAsync(string userId, string? displayName, string? email, CancellationToken token)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? email ?? string.Empty : displayName.Trim();
        if (name.Length > 120) name = name[..120];
        if (projected.TryGetValue(userId, out var known) && known == name) return;
        // A new person gets the row the authored-record foreign keys need; a known one only follows a changed name.
        const string sql =
            """
            INSERT INTO access.users(
                id,"DisplayName","Email","IsActive","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled",
                "LockoutEnabled","AccessFailedCount")
            VALUES (@userId,@name,@email,true,false,false,false,false,0)
            ON CONFLICT (id) DO UPDATE SET "DisplayName"=EXCLUDED."DisplayName"
            WHERE access.users."DisplayName" IS DISTINCT FROM EXCLUDED."DisplayName"
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { userId, name, email }, cancellationToken: token));
        projected[userId] = name;
    }

    public async Task ProjectOrganizationAsync(Guid organizationId, string? name, string? slug, CancellationToken token)
    {
        var named = !string.IsNullOrWhiteSpace(name);
        var label = named ? name!.Trim() : "Organization";
        if (label.Length > 120) label = label[..120];
        if (projectedOrganizations.TryGetValue(organizationId, out var known) && known == label) return;
        // A new organization gets the row its records' foreign keys need; a named one follows Identity's name. A slug
        // the token lacks, or one another organization holds, falls back to one derived from the id.
        var fallbackSlug = "org-" + organizationId.ToString("N")[..12];
        var candidate = slug is { Length: >= 1 and <= 64 } && Regex.IsMatch(slug, "^[a-z0-9]+(-[a-z0-9]+)*$")
            ? slug
            : fallbackSlug;
        try
        {
            await InsertOrganizationAsync(organizationId, label, candidate, named, token);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation && candidate != fallbackSlug)
        {
            await InsertOrganizationAsync(organizationId, label, fallbackSlug, named, token);
        }
        projectedOrganizations[organizationId] = label;
    }

    private async Task InsertOrganizationAsync(
        Guid organizationId,
        string name,
        string slug,
        bool named,
        CancellationToken token)
    {
        const string sql =
            """
            INSERT INTO access.organizations(id,name,slug) VALUES (@organizationId,@name,@slug)
            ON CONFLICT (id) DO UPDATE SET name=EXCLUDED.name
            WHERE @named AND access.organizations.name IS DISTINCT FROM EXCLUDED.name
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { organizationId, name, slug, named }, cancellationToken: token));
    }

    /// <summary>An organization's stored name and slug.</summary>
    private sealed class OrganizationRow
    {
        public string Name { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
    }
}
