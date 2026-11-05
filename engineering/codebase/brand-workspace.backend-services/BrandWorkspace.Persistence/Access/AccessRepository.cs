using System.Collections.Concurrent;
using BrandWorkspace.Application.Access;
using Npgsql;

namespace BrandWorkspace.Persistence.Access;

/// <summary>Uses parameterized PostgreSQL statements for Desk's organizations and its projection of signed-in people.</summary>
public sealed class AccessRepository(NpgsqlDataSource dataSource) : IAccessRepository
{
    private readonly ConcurrentDictionary<string, string> projected = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, string> projectedOrganizations = new();

    public async Task<OrganizationAccess?> FindMembershipAsync(Guid organizationId, string userId, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT role FROM access.memberships WHERE organization_id=@organization AND user_id=@user");
        command.Parameters.AddWithValue("organization", organizationId);
        command.Parameters.AddWithValue("user", userId);
        return await command.ExecuteScalarAsync(token) is string role
            ? new OrganizationAccess(organizationId, userId, role, role is "owner" or "editor", role == "owner") : null;
    }

    public async Task<Organization?> FindOrganizationAsync(Guid organizationId, string role, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("SELECT name,slug FROM access.organizations WHERE id=@id");
        command.Parameters.AddWithValue("id", organizationId);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new Organization(organizationId, reader.GetString(0), reader.GetString(1), role) : null;
    }

    public async Task ProjectUserAsync(string userId, string? displayName, string? email, CancellationToken token)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? email ?? string.Empty : displayName.Trim();
        if (name.Length > 120) name = name[..120];
        if (projected.TryGetValue(userId, out var known) && known == name) return;
        // A new person gets the row the authored-record foreign keys need; a known one only follows a changed name.
        await using var command = dataSource.CreateCommand("""
            INSERT INTO access.users(id,"DisplayName","Email","IsActive","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount")
            VALUES (@id,@name,@email,true,false,false,false,false,0)
            ON CONFLICT (id) DO UPDATE SET "DisplayName"=EXCLUDED."DisplayName"
            WHERE access.users."DisplayName" IS DISTINCT FROM EXCLUDED."DisplayName"
            """);
        command.Parameters.AddWithValue("id", userId);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("email", (object?)email ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(token);
        projected[userId] = name;
    }

    public async Task ProjectOrganizationAsync(Guid organizationId, string? name, string? slug, CancellationToken token)
    {
        var named = !string.IsNullOrWhiteSpace(name);
        var label = named ? name!.Trim() : "Organization";
        if (label.Length > 120) label = label[..120];
        if (projectedOrganizations.TryGetValue(organizationId, out var known) && known == label) return;
        // A new organization gets the row its records' foreign keys need; a named one follows Identity's name. A slug the
        // token lacks, or one another organization holds, falls back to one derived from the id.
        var fallbackSlug = "org-" + organizationId.ToString("N")[..12];
        var candidate = slug is { Length: >= 1 and <= 64 } && System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9]+(-[a-z0-9]+)*$") ? slug : fallbackSlug;
        try
        {
            await InsertOrganizationAsync(organizationId, label, candidate, named, token);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation && candidate != fallbackSlug)
        {
            await InsertOrganizationAsync(organizationId, label, fallbackSlug, named, token);
        }
        projectedOrganizations[organizationId] = label;
    }

    private async Task InsertOrganizationAsync(Guid organizationId, string name, string slug, bool named, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO access.organizations(id,name,slug) VALUES (@id,@name,@slug)
            ON CONFLICT (id) DO UPDATE SET name=EXCLUDED.name
            WHERE @named AND access.organizations.name IS DISTINCT FROM EXCLUDED.name
            """);
        command.Parameters.AddWithValue("id", organizationId);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("slug", slug);
        command.Parameters.AddWithValue("named", named);
        await command.ExecuteNonQueryAsync(token);
    }
}
