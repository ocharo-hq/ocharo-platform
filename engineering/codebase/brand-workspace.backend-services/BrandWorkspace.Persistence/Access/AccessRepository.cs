using System.Collections.Concurrent;
using BrandWorkspace.Application.Access;
using Npgsql;

namespace BrandWorkspace.Persistence.Access;

/// <summary>Uses parameterized PostgreSQL statements for Desk's organizations and its projection of signed-in people.</summary>
public sealed class AccessRepository(NpgsqlDataSource dataSource) : IAccessRepository
{
    private readonly ConcurrentDictionary<string, string> projected = new(StringComparer.Ordinal);

    public async Task<bool> IsSessionActiveAsync(Guid sessionId, string userId, DateTimeOffset now, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT EXISTS(SELECT 1 FROM access.sessions s JOIN access.users u ON u.id=s.user_id
            WHERE s.id=@id AND s.user_id=@user AND s.revoked_at IS NULL AND s.expires_at>@now
              AND u."IsActive" AND u."SecurityStamp"=s.security_stamp)
            """);
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("now", now);
        return (bool)(await command.ExecuteScalarAsync(token))!;
    }

    public async Task<OrganizationAccess?> FindMembershipAsync(Guid organizationId, string userId, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT role FROM access.memberships WHERE organization_id=@organization AND user_id=@user");
        command.Parameters.AddWithValue("organization", organizationId);
        command.Parameters.AddWithValue("user", userId);
        return await command.ExecuteScalarAsync(token) is string role
            ? new OrganizationAccess(organizationId, userId, role, role is "owner" or "editor", role == "owner") : null;
    }

    public async Task<OperatorProfile?> FindProfileAsync(string userId, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("""SELECT "DisplayName","Email" FROM access.users WHERE id=@user""");
        command.Parameters.AddWithValue("user", userId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return new OperatorProfile(reader.IsDBNull(0) ? null : reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
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
}
