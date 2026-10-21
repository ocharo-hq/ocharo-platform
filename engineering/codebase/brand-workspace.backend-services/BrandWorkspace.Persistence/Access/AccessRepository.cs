using BrandWorkspace.Application.Access;
using Npgsql;

namespace BrandWorkspace.Persistence.Access;

/// <summary>Uses parameterized PostgreSQL statements for session and membership authority.</summary>
public sealed class AccessRepository(NpgsqlDataSource dataSource) : IAccessRepository
{
    public async Task CreateSessionAsync(Guid sessionId, string userId, string securityStamp, DateTimeOffset expiresAt,
        Guid? replacedSessionId, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO access.sessions(id,user_id,security_stamp,expires_at)
            VALUES (@id,@user,@stamp,@expires)
            """, connection, transaction);
        insert.Parameters.AddWithValue("id", sessionId);
        insert.Parameters.AddWithValue("user", userId);
        insert.Parameters.AddWithValue("stamp", securityStamp);
        insert.Parameters.AddWithValue("expires", expiresAt);
        await insert.ExecuteNonQueryAsync(token);
        if (replacedSessionId is { } previous)
        {
            await using var revoke = new NpgsqlCommand(
                "UPDATE access.sessions SET revoked_at=now() WHERE id=@id AND revoked_at IS NULL", connection, transaction);
            revoke.Parameters.AddWithValue("id", previous);
            await revoke.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

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

    public async Task RevokeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE access.sessions SET revoked_at=@now WHERE id=@id AND revoked_at IS NULL");
        command.Parameters.AddWithValue("id", sessionId);
        command.Parameters.AddWithValue("now", now);
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task<IReadOnlyList<Organization>> ListOrganizationsAsync(string userId, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT o.id,o.name,o.slug,m.role FROM access.organizations o
            JOIN access.memberships m ON m.organization_id=o.id WHERE m.user_id=@user ORDER BY o.name,o.id
            """);
        command.Parameters.AddWithValue("user", userId);
        await using var reader = await command.ExecuteReaderAsync(token);
        var result = new List<Organization>();
        while (await reader.ReadAsync(token))
            result.Add(new Organization(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        return result;
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

    public async Task<Organization?> CreateOrganizationAsync(string name, string slug, string userId, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var id = Guid.NewGuid();
        try
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO access.organizations(id,name,slug) VALUES (@id,@name,@slug);
                INSERT INTO access.memberships(organization_id,user_id,role) VALUES (@id,@user,'owner');
                """, connection, transaction);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("slug", slug);
            command.Parameters.AddWithValue("user", userId);
            await command.ExecuteNonQueryAsync(token);
            await transaction.CommitAsync(token);
            return new Organization(id, name, slug, "owner");
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(token);
            return null;
        }
    }
}
