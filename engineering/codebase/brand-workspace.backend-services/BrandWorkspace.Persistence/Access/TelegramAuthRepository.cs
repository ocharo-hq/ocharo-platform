using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;

namespace BrandWorkspace.Persistence.Access;

/// <summary>Persists Telegram links and encrypted codes under transaction-scoped advisory locks.</summary>
/// <remarks>The caller keeps the transaction across SDK verification and session insertion, so only one login consumes a code.</remarks>
public sealed class TelegramAuthRepository(NpgsqlDataSource source, IDataProtectionProvider protection, TimeProvider clock)
    : IOtpRepository, IAsyncDisposable
{
    private readonly IDataProtector protector = protection.CreateProtector("Ocharo.Desk.Telegram.Codes.v1");
    private NpgsqlConnection? connection;
    private NpgsqlTransaction? transaction;

    public async Task BeginAsync(string key, CancellationToken ct)
    {
        if (transaction is not null) throw new InvalidOperationException("A Telegram transaction is already active.");
        connection = await source.OpenConnectionAsync(ct);
        transaction = await connection.BeginTransactionAsync(ct);
        await using var command = Command("SELECT pg_advisory_xact_lock(hashtextextended(@key, 8264009))", ("key", key));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (transaction is null) throw new InvalidOperationException("A Telegram transaction is required.");
        await transaction.CommitAsync(ct);
        await transaction.DisposeAsync();
        transaction = null;
    }

    public async Task<TelegramLink?> FindByUserAsync(string userId, CancellationToken ct)
    {
        await using var command = Command("SELECT user_id,telegram_id,phone FROM access.telegram_links WHERE user_id=@user", ("user", userId));
        return await ReadLinkAsync(command, ct);
    }

    public async Task<TelegramLink?> FindByPhoneAsync(string phone, CancellationToken ct)
    {
        await using var command = Command("SELECT user_id,telegram_id,phone FROM access.telegram_links WHERE phone=@phone", ("phone", phone));
        return await ReadLinkAsync(command, ct);
    }

    public async Task CreateLinkRequestAsync(string hash, string userId, Guid session, DateTimeOffset expires, CancellationToken ct)
    {
        RequireTransaction();
        await using var command = Command("""
            DELETE FROM access.telegram_link_requests WHERE user_id=@user OR expires_at<@now;
            INSERT INTO access.telegram_link_requests(token_hash,user_id,session_id,expires_at) VALUES (@hash,@user,@session,@expires);
            """, ("user", userId), ("hash", hash), ("session", session), ("expires", expires), ("now", clock.GetUtcNow()));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> AttachLinkRequestAsync(string hash, long telegramId, CancellationToken ct)
    {
        RequireTransaction();
        await using var find = Command("""
            SELECT EXISTS(SELECT 1 FROM access.telegram_link_requests r
                JOIN access.sessions s ON s.id=r.session_id JOIN access.users u ON u.id=r.user_id
                WHERE r.token_hash=@hash AND NOT r.consumed AND r.expires_at>@now
                  AND (r.telegram_id IS NULL OR r.telegram_id=@chat)
                  AND s.revoked_at IS NULL AND s.expires_at>@now AND u."IsActive" AND u."SecurityStamp"=s.security_stamp)
            """, ("hash", hash), ("chat", telegramId), ("now", clock.GetUtcNow()));
        if (await find.ExecuteScalarAsync(ct) is not true) return false;
        await using var command = Command("""
            UPDATE access.telegram_link_requests SET consumed=true WHERE telegram_id=@chat AND token_hash<>@hash;
            UPDATE access.telegram_link_requests SET telegram_id=@chat WHERE token_hash=@hash;
            """, ("chat", telegramId), ("hash", hash));
        await command.ExecuteNonQueryAsync(ct);
        return true;
    }

    public async Task<bool> CompleteLinkAsync(long telegramId, string phone, CancellationToken ct)
    {
        RequireTransaction();
        await using var command = Command("""
            INSERT INTO access.telegram_links(user_id,telegram_id,phone,linked_at)
            SELECT r.user_id,@chat,@phone,@now FROM access.telegram_link_requests r
            JOIN access.sessions s ON s.id=r.session_id JOIN access.users u ON u.id=r.user_id
            WHERE r.telegram_id=@chat AND NOT r.consumed AND r.expires_at>@now
              AND s.revoked_at IS NULL AND s.expires_at>@now AND u."IsActive" AND u."SecurityStamp"=s.security_stamp
            ON CONFLICT DO NOTHING RETURNING user_id
            """, ("chat", telegramId), ("phone", phone), ("now", clock.GetUtcNow()));
        var linked = await command.ExecuteScalarAsync(ct) is string;
        await using var consume = Command("UPDATE access.telegram_link_requests SET consumed=true WHERE telegram_id=@chat", ("chat", telegramId));
        await consume.ExecuteNonQueryAsync(ct);
        return linked;
    }

    public async Task RemoveLinkAndSessionsAsync(string userId, CancellationToken ct)
    {
        RequireTransaction();
        await using var command = Command("""
            UPDATE access.telegram_codes SET consumed=true WHERE subject IN (SELECT phone FROM access.telegram_links WHERE user_id=@user);
            DELETE FROM access.telegram_links WHERE user_id=@user;
            DELETE FROM access.telegram_link_requests WHERE user_id=@user;
            UPDATE access.users SET "SecurityStamp"=@stamp,"ConcurrencyStamp"=@concurrency WHERE id=@user;
            UPDATE access.sessions SET revoked_at=@now WHERE user_id=@user AND revoked_at IS NULL;
            """, ("user", userId), ("stamp", Guid.NewGuid().ToString()), ("concurrency", Guid.NewGuid().ToString()), ("now", clock.GetUtcNow()));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> CreateSessionAsync(Guid id, AccessUser user, DateTimeOffset expires, Guid? previous, CancellationToken ct)
    {
        RequireTransaction();
        await using var command = Command("""
            INSERT INTO access.sessions(id,user_id,security_stamp,expires_at)
            SELECT @id,id,"SecurityStamp",@expires FROM access.users
            WHERE id=@user AND "IsActive" AND NOT "TwoFactorEnabled" AND "SecurityStamp"=@stamp
              AND (NOT "LockoutEnabled" OR "LockoutEnd" IS NULL OR "LockoutEnd"<=@now)
            RETURNING id
            """, ("id", id), ("user", user.Id), ("stamp", user.SecurityStamp!), ("expires", expires), ("now", clock.GetUtcNow()));
        if (await command.ExecuteScalarAsync(ct) is not Guid) return false;
        if (previous is { } old)
        {
            await using var revoke = Command("UPDATE access.sessions SET revoked_at=@now WHERE id=@id AND revoked_at IS NULL", ("id", old), ("now", clock.GetUtcNow()));
            await revoke.ExecuteNonQueryAsync(ct);
        }
        return true;
    }

    public async Task<bool> HasRecentPendingAsync(string subject, string scope, TimeSpan window, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await using var command = Command("SELECT EXISTS(SELECT 1 FROM access.telegram_codes WHERE subject=@subject AND scope=@scope AND created_at>@since)",
            ("subject", subject), ("scope", scope), ("since", clock.GetUtcNow()-window));
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    public async Task SaveAsync(OtpRecord record, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await using var command = Command("""
            UPDATE access.telegram_codes SET consumed=true WHERE subject=@subject AND scope=@scope;
            DELETE FROM access.telegram_codes WHERE expires_at<@cleanup;
            INSERT INTO access.telegram_codes(id,subject,scope,protected_code,created_at,expires_at)
            VALUES (@id,@subject,@scope,@code,@created,@expires)
            """, ("id", record.Id), ("subject", record.Subject), ("scope", record.Scope), ("code", protector.Protect(record.Code)),
            ("created", record.CreatedAt), ("expires", record.ExpiresAt), ("cleanup", clock.GetUtcNow().AddDays(-1)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<OtpRecord?> FindLatestPendingAsync(string subject, string scope, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await using var command = Command("""
            SELECT id,protected_code,created_at,expires_at,attempts FROM access.telegram_codes
            WHERE subject=@subject AND scope=@scope AND NOT consumed ORDER BY created_at DESC LIMIT 1 FOR UPDATE
            """, ("subject", subject), ("scope", scope));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new OtpRecord { Id=reader.GetGuid(0), Subject=subject, Scope=scope, Code=protector.Unprotect(reader.GetString(1)),
            CreatedAt=reader.GetFieldValue<DateTimeOffset>(2), ExpiresAt=reader.GetFieldValue<DateTimeOffset>(3), Attempts=reader.GetInt32(4), Consumed=false };
    }

    public async Task IncrementAttemptsAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await using var command = Command("UPDATE access.telegram_codes SET attempts=attempts+1 WHERE id=@id", ("id", recordId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkConsumedAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await using var command = Command("UPDATE access.telegram_codes SET consumed=true WHERE id=@id", ("id", recordId));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ConsumePendingAsync(string subject, string scope, CancellationToken ct)
    {
        RequireTransaction();
        await using var command = Command("UPDATE access.telegram_codes SET consumed=true WHERE subject=@subject AND scope=@scope", ("subject", subject), ("scope", scope));
        await command.ExecuteNonQueryAsync(ct);
    }

    private NpgsqlCommand Command(string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection is null ? source.CreateCommand(sql) : new NpgsqlCommand(sql, connection, transaction);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return command;
    }

    private void RequireTransaction()
    {
        if (transaction is null) throw new InvalidOperationException("Telegram state changes require an explicit locked transaction.");
    }

    private static async Task<TelegramLink?> ReadLinkAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new TelegramLink(reader.GetString(0), reader.GetInt64(1), reader.GetString(2)) : null;
    }

    public async ValueTask DisposeAsync()
    {
        if (transaction is not null) await transaction.DisposeAsync();
        if (connection is not null) await connection.DisposeAsync();
        transaction = null;
        connection = null;
    }
}

public sealed record TelegramLink(string UserId, long TelegramId, string Phone);
