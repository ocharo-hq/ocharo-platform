using BrandWorkspace.Application.Sso;
using Npgsql;

namespace BrandWorkspace.Persistence.Sso;

/// <summary>Stores credential digests; each consume/rotation is one atomic SQL statement.</summary>
public sealed class SsoGrantStore(NpgsqlDataSource database) : ISsoGrantStore
{
    public async Task StoreCodeAsync(SsoAuthorizationCode code, CancellationToken ct)
    {
        await using var command = database.CreateCommand("""
            INSERT INTO access.sso_codes(code_hash,client_id,redirect_uri,challenge,user_id,session_id,organization_id,expires_at,session_expires_at)
            VALUES (@hash,@client,@redirect,@challenge,@user,@session,@org,@expires,@sessionExpires)
            """);
        command.Parameters.AddWithValue("hash", code.Hash);
        command.Parameters.AddWithValue("client", code.ClientId);
        command.Parameters.AddWithValue("redirect", code.RedirectUri);
        command.Parameters.AddWithValue("challenge", code.Challenge);
        command.Parameters.AddWithValue("user", code.SsoGrant.UserId);
        command.Parameters.AddWithValue("session", code.SsoGrant.SessionId);
        command.Parameters.AddWithValue("org", code.SsoGrant.OrganizationId);
        command.Parameters.AddWithValue("expires", code.ExpiresAt);
        command.Parameters.AddWithValue("sessionExpires", code.SsoGrant.ExpiresAt);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<SsoGrant?> ConsumeCodeAsync(string hash, string client, string redirect, string challenge,
        string newHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = database.CreateCommand("""
            WITH consumed AS (
                UPDATE access.sso_codes SET consumed_at=@now
                WHERE code_hash=@hash AND client_id=@client AND redirect_uri=@redirect AND challenge=@challenge
                  AND consumed_at IS NULL AND expires_at>@now AND session_expires_at>@now
                RETURNING user_id,session_id,organization_id,session_expires_at
            )
            INSERT INTO access.sso_grants(token_hash,client_id,user_id,session_id,organization_id,expires_at)
            SELECT @newHash,@client,user_id,session_id,organization_id,session_expires_at FROM consumed
            RETURNING user_id,session_id,organization_id,expires_at
            """);
        Bind(command, hash, client, now);
        command.Parameters.AddWithValue("redirect", redirect);
        command.Parameters.AddWithValue("challenge", challenge);
        command.Parameters.AddWithValue("newHash", newHash);
        return await ReadAsync(command, ct);
    }

    public async Task<SsoGrant?> FindGrantAsync(string hash, string client, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = database.CreateCommand("""
            SELECT user_id,session_id,organization_id,expires_at FROM access.sso_grants
            WHERE token_hash=@hash AND client_id=@client AND expires_at>@now AND revoked_at IS NULL
            """);
        Bind(command, hash, client, now);
        return await ReadAsync(command, ct);
    }

    public async Task<SsoGrant?> RotateAsync(string hash, string client, string newHash, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = database.CreateCommand("""
            WITH consumed AS (
                UPDATE access.sso_grants SET revoked_at=@now
                WHERE token_hash=@hash AND client_id=@client AND expires_at>@now AND revoked_at IS NULL
                RETURNING user_id,session_id,organization_id,expires_at
            )
            INSERT INTO access.sso_grants(token_hash,client_id,user_id,session_id,organization_id,expires_at)
            SELECT @newHash,@client,user_id,session_id,organization_id,expires_at FROM consumed
            RETURNING user_id,session_id,organization_id,expires_at
            """);
        Bind(command, hash, client, now);
        command.Parameters.AddWithValue("newHash", newHash);
        return await ReadAsync(command, ct);
    }

    public async Task RevokeAsync(string hash, string client, DateTimeOffset now, CancellationToken ct)
    {
        await using var command = database.CreateCommand("""
            UPDATE access.sso_grants SET revoked_at=@now WHERE token_hash=@hash AND client_id=@client AND revoked_at IS NULL
            """);
        Bind(command, hash, client, now);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void Bind(NpgsqlCommand command, string hash, string client, DateTimeOffset now)
    {
        command.Parameters.AddWithValue("hash", hash);
        command.Parameters.AddWithValue("client", client);
        command.Parameters.AddWithValue("now", now);
    }

    private static async Task<SsoGrant?> ReadAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new(reader.GetString(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetFieldValue<DateTimeOffset>(3)) : null;
    }
}
