using Npgsql;
using NpgsqlTypes;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Sourcing.Models;
using BrandWorkspace.Application.Sourcing.Repositories;
using BrandWorkspace.Domain.Sourcing.Models;

namespace BrandWorkspace.Persistence.Sourcing.Repositories;

/// <summary>Persists suppliers, routes, people and content-addressed media inside one organization in PostgreSQL.</summary>
public sealed class SourcingRepository(NpgsqlDataSource dataSource, TimeProvider time) : ISourcingRepository
{
    private const string SupplierColumns = "id, import_key, name, stage, country, city, website, notes, revision, created_at, updated_at";
    private const string PersonColumns = "id, supplier_id, name, role, notes, revision, created_at, updated_at";
    private const string RouteColumns =
        "id, supplier_id, person_id, channel, destination, label, source_url, verified, is_primary, notes, revision, created_at, updated_at";
    private const string MediaColumns =
        "id, supplier_id, sha256, content_type, byte_size, width, height, has_thumbnail, capture, original_name, caption, " +
        "source_label, source_message_id, received_at, taken_at, has_location, revision, created_at, updated_at";

    public async Task<IReadOnlyList<SupplierSummary>> GetSuppliersAsync(
        Guid organizationId, string? query, string? stage, int limit, int offset, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null, """
            SELECT s.id, s.import_key, s.name, s.stage, s.country, s.city, s.revision, s.updated_at,
                pr.channel, pr.destination,
                (SELECT count(*) FROM sourcing.contact_routes r WHERE r.organization_id=s.organization_id AND r.supplier_id=s.id)::int,
                (SELECT count(*) FROM sourcing.people p WHERE p.organization_id=s.organization_id AND p.supplier_id=s.id)::int,
                (SELECT count(*) FROM sourcing.media m WHERE m.organization_id=s.organization_id AND m.supplier_id=s.id)::int,
                (SELECT m.id FROM sourcing.media m
                    WHERE m.organization_id=s.organization_id AND m.supplier_id=s.id AND m.has_thumbnail
                    ORDER BY m.received_at DESC NULLS LAST, m.created_at DESC, m.id LIMIT 1)
            FROM sourcing.suppliers s
            LEFT JOIN sourcing.contact_routes pr
                ON pr.organization_id=s.organization_id AND pr.supplier_id=s.id AND pr.is_primary
            WHERE s.organization_id=$1
                AND ($2::text IS NULL
                    OR s.name ILIKE $2 ESCAPE '\' OR s.city ILIKE $2 ESCAPE '\' OR s.import_key ILIKE $2 ESCAPE '\'
                    OR EXISTS (SELECT 1 FROM sourcing.contact_routes r
                        WHERE r.organization_id=s.organization_id AND r.supplier_id=s.id AND r.destination ILIKE $2 ESCAPE '\')
                    OR EXISTS (SELECT 1 FROM sourcing.people p
                        WHERE p.organization_id=s.organization_id AND p.supplier_id=s.id AND p.name ILIKE $2 ESCAPE '\'))
                AND ($3::text IS NULL OR s.stage=$3)
            ORDER BY lower(s.name), s.id
            LIMIT $4 OFFSET $5
            """, organizationId, query is null ? null : "%" + EscapeLike(query) + "%", stage, limit, offset);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        var suppliers = new List<SupplierSummary>();
        while (await reader.ReadAsync(token))
        {
            suppliers.Add(new SupplierSummary(
                reader.GetGuid(0),
                NullableString(reader, 1),
                reader.GetString(2),
                reader.GetString(3),
                NullableString(reader, 4),
                NullableString(reader, 5),
                reader.IsDBNull(8) ? null : new SupplierRouteSummary(reader.GetString(8), reader.GetString(9)),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.IsDBNull(13) ? null : reader.GetGuid(13),
                reader.GetInt32(6),
                reader.GetFieldValue<DateTimeOffset>(7)));
        }
        return suppliers;
    }

    public async Task<Supplier> GetSupplierAsync(Guid organizationId, Guid supplierId, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        return await ReadSupplierAsync(connection, null, organizationId, supplierId, token)
            ?? throw NotFound("supplier");
    }

    public async Task<Supplier?> FindSupplierByKeyAsync(Guid organizationId, string key, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null,
            "SELECT id FROM sourcing.suppliers WHERE organization_id=$1 AND import_key=$2", organizationId, key);
        return await command.ExecuteScalarAsync(token) is Guid id
            ? await ReadSupplierAsync(connection, null, organizationId, id, token)
            : null;
    }

    public async Task<Supplier> CreateSupplierAsync(
        Guid organizationId, string actor, string? key, SupplierInput input, CancellationToken token)
    {
        DateTimeOffset now = time.GetUtcNow();
        Guid id = Guid.NewGuid();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using (NpgsqlCommand insert = Command(connection, null, """
            INSERT INTO sourcing.suppliers(organization_id, id, import_key, name, stage, country, city, website, notes,
                revision, created_by, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, 1, $10, $11, $11)
            """, organizationId, id, key, input.Name, input.Stage, input.Country, input.City, input.Website, input.Notes, actor, now))
        {
            await ExecuteAsync(insert, "A supplier with this import key already exists.", token);
        }
        return (await ReadSupplierAsync(connection, null, organizationId, id, token))!;
    }

    public async Task<Supplier> UpdateSupplierAsync(
        Guid organizationId, Guid supplierId, int expectedRevision, SupplierInput input, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using (NpgsqlCommand update = Command(connection, null, """
            UPDATE sourcing.suppliers
            SET name=$4, stage=$5, country=$6, city=$7, website=$8, notes=$9, revision=revision+1, updated_at=$10
            WHERE organization_id=$1 AND id=$2 AND revision=$3
            """, organizationId, supplierId, expectedRevision, input.Name, input.Stage, input.Country, input.City,
            input.Website, input.Notes, time.GetUtcNow()))
        {
            if (await update.ExecuteNonQueryAsync(token) == 0)
                throw await MissingOrConflictAsync(connection, null, "suppliers", organizationId, supplierId, "supplier", token);
        }
        return (await ReadSupplierAsync(connection, null, organizationId, supplierId, token))!;
    }

    public async Task<SupplierPerson> CreatePersonAsync(
        Guid organizationId, string actor, Guid supplierId, PersonInput input, CancellationToken token)
    {
        DateTimeOffset now = time.GetUtcNow();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        await LockSupplierAsync(connection, transaction, organizationId, supplierId, token);
        await using NpgsqlCommand insert = Command(connection, transaction, $"""
            INSERT INTO sourcing.people(organization_id, id, supplier_id, name, role, notes, revision, created_by, created_at, updated_at)
            VALUES ($1, $2, $3, $4, $5, $6, 1, $7, $8, $8)
            RETURNING {PersonColumns}
            """, organizationId, Guid.NewGuid(), supplierId, input.Name, input.Role, input.Notes, actor, now);
        SupplierPerson person = await ReadSingleAsync(insert, ReadPerson, token);
        await transaction.CommitAsync(token);
        return person;
    }

    public async Task<SupplierPerson> UpdatePersonAsync(
        Guid organizationId, Guid personId, int expectedRevision, PersonInput input, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand update = Command(connection, null, $"""
            UPDATE sourcing.people SET name=$4, role=$5, notes=$6, revision=revision+1, updated_at=$7
            WHERE organization_id=$1 AND id=$2 AND revision=$3
            RETURNING {PersonColumns}
            """, organizationId, personId, expectedRevision, input.Name, input.Role, input.Notes, time.GetUtcNow());
        return await ReadOptionalAsync(update, ReadPerson, token)
            ?? throw await MissingOrConflictAsync(connection, null, "people", organizationId, personId, "person", token);
    }

    public async Task DeletePersonAsync(Guid organizationId, Guid personId, int expectedRevision, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        await using (NpgsqlCommand current = Command(connection, transaction,
            "SELECT revision FROM sourcing.people WHERE organization_id=$1 AND id=$2 FOR UPDATE", organizationId, personId))
        {
            if (await current.ExecuteScalarAsync(token) is not int revision) throw NotFound("person");
            if (revision != expectedRevision) throw Conflict("person");
        }
        // Routes stay with the supplier; they only lose the person reference, which counts as a change.
        await using (NpgsqlCommand release = Command(connection, transaction, """
            UPDATE sourcing.contact_routes SET person_id=NULL, revision=revision+1, updated_at=$3
            WHERE organization_id=$1 AND person_id=$2
            """, organizationId, personId, time.GetUtcNow()))
        {
            await release.ExecuteNonQueryAsync(token);
        }
        await using (NpgsqlCommand delete = Command(connection, transaction,
            "DELETE FROM sourcing.people WHERE organization_id=$1 AND id=$2", organizationId, personId))
        {
            await delete.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    public async Task<ContactRoute> CreateRouteAsync(
        Guid organizationId, string actor, Guid supplierId, RouteInput input, CancellationToken token)
    {
        DateTimeOffset now = time.GetUtcNow();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        await LockSupplierAsync(connection, transaction, organizationId, supplierId, token);
        await RequirePersonAsync(connection, transaction, organizationId, supplierId, input.PersonId, token);
        if (input.IsPrimary) await ClearPrimaryAsync(connection, transaction, organizationId, supplierId, null, now, token);
        await using NpgsqlCommand insert = Command(connection, transaction, $"""
            INSERT INTO sourcing.contact_routes(organization_id, id, supplier_id, person_id, channel, destination, label,
                source_url, verified, is_primary, notes, revision, created_by, created_at, updated_at)
            VALUES ($1, $2, $3, $4::uuid, $5, $6, $7, $8, $9, $10, $11, 1, $12, $13, $13)
            RETURNING {RouteColumns}
            """, organizationId, Guid.NewGuid(), supplierId, input.PersonId, input.Channel, input.Destination, input.Label,
            input.SourceUrl, input.Verified, input.IsPrimary, input.Notes, actor, now);
        ContactRoute route = await ReadSingleAsync(insert, ReadRoute, token, "This contact route already exists for the supplier.");
        await transaction.CommitAsync(token);
        return route;
    }

    public async Task<ContactRoute> UpdateRouteAsync(
        Guid organizationId, Guid routeId, int expectedRevision, RouteInput input, CancellationToken token)
    {
        DateTimeOffset now = time.GetUtcNow();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        Guid supplierId;
        await using (NpgsqlCommand owner = Command(connection, transaction,
            "SELECT supplier_id FROM sourcing.contact_routes WHERE organization_id=$1 AND id=$2", organizationId, routeId))
        {
            supplierId = await owner.ExecuteScalarAsync(token) is Guid id ? id : throw NotFound("contact route");
        }
        await LockSupplierAsync(connection, transaction, organizationId, supplierId, token);
        await RequirePersonAsync(connection, transaction, organizationId, supplierId, input.PersonId, token);
        await using (NpgsqlCommand current = Command(connection, transaction,
            "SELECT revision FROM sourcing.contact_routes WHERE organization_id=$1 AND id=$2 FOR UPDATE", organizationId, routeId))
        {
            if (await current.ExecuteScalarAsync(token) is not int revision) throw NotFound("contact route");
            if (revision != expectedRevision) throw Conflict("contact route");
        }
        if (input.IsPrimary) await ClearPrimaryAsync(connection, transaction, organizationId, supplierId, routeId, now, token);
        await using NpgsqlCommand update = Command(connection, transaction, $"""
            UPDATE sourcing.contact_routes
            SET person_id=$3::uuid, channel=$4, destination=$5, label=$6, source_url=$7, verified=$8, is_primary=$9, notes=$10,
                revision=revision+1, updated_at=$11
            WHERE organization_id=$1 AND id=$2
            RETURNING {RouteColumns}
            """, organizationId, routeId, input.PersonId, input.Channel, input.Destination, input.Label, input.SourceUrl,
            input.Verified, input.IsPrimary, input.Notes, now);
        ContactRoute route = await ReadSingleAsync(update, ReadRoute, token, "This contact route already exists for the supplier.");
        await transaction.CommitAsync(token);
        return route;
    }

    public async Task DeleteRouteAsync(Guid organizationId, Guid routeId, int expectedRevision, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand delete = Command(connection, null,
            "DELETE FROM sourcing.contact_routes WHERE organization_id=$1 AND id=$2 AND revision=$3",
            organizationId, routeId, expectedRevision);
        if (await delete.ExecuteNonQueryAsync(token) == 0)
            throw await MissingOrConflictAsync(connection, null, "contact_routes", organizationId, routeId, "contact route", token);
    }

    public async Task<IReadOnlyList<SourcingMedia>> GetMediaAsync(
        Guid organizationId, Guid? supplierId, bool unassigned, int limit, int offset, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null, $"""
            SELECT {MediaColumns}
            FROM sourcing.media
            WHERE organization_id=$1
                AND ($2::uuid IS NULL OR supplier_id=$2::uuid)
                AND (NOT $3 OR supplier_id IS NULL)
            ORDER BY received_at DESC NULLS LAST, created_at DESC, id
            LIMIT $4 OFFSET $5
            """, organizationId, supplierId, unassigned, limit, offset);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        var media = new List<SourcingMedia>();
        while (await reader.ReadAsync(token)) media.Add(ReadMedia(reader));
        return media;
    }

    public async Task<SourcingMediaTotals> GetMediaTotalsAsync(Guid organizationId, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null,
            "SELECT count(*)::int, (count(*) FILTER (WHERE supplier_id IS NULL))::int FROM sourcing.media WHERE organization_id=$1",
            organizationId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        return new SourcingMediaTotals(reader.GetInt32(0), reader.GetInt32(1));
    }

    public async Task<SourcingMedia?> FindMediaBySha256Async(Guid organizationId, string sha256, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null,
            $"SELECT {MediaColumns} FROM sourcing.media WHERE organization_id=$1 AND sha256=$2", organizationId, sha256);
        return await ReadOptionalAsync(command, ReadMedia, token);
    }

    public async Task<IReadOnlyList<Guid>> FindNearDuplicatesAsync(
        Guid organizationId, long perceptualHash, int maximumDistance, Guid excludedMediaId, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null, """
            SELECT id FROM sourcing.media
            WHERE organization_id=$1 AND perceptual_hash IS NOT NULL AND id<>$3
                AND bit_count(int8send(perceptual_hash # $2)) <= $4
            ORDER BY bit_count(int8send(perceptual_hash # $2)), created_at, id
            LIMIT 20
            """, organizationId, perceptualHash, excludedMediaId, (long)maximumDistance);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        var ids = new List<Guid>();
        while (await reader.ReadAsync(token)) ids.Add(reader.GetGuid(0));
        return ids;
    }

    public async Task<SourcingMediaUpload> CreateMediaAsync(
        Guid organizationId, string actor, NewSourcingMedia media, CancellationToken token)
    {
        DateTimeOffset now = time.GetUtcNow();
        InspectedMedia content = media.Content;
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        await RequireSupplierReferenceAsync(connection, transaction, organizationId, media.SupplierId, token);
        await using (NpgsqlCommand bytes = Command(connection, transaction, """
            INSERT INTO sourcing.media_contents(organization_id, sha256, bytes, thumbnail)
            VALUES ($1, $2, $3, $4::bytea)
            ON CONFLICT (organization_id, sha256) DO NOTHING
            """, organizationId, content.Sha256, content.Bytes, content.Thumbnail))
        {
            await bytes.ExecuteNonQueryAsync(token);
        }
        await using NpgsqlCommand insert = Command(connection, transaction, $"""
            INSERT INTO sourcing.media(organization_id, id, supplier_id, sha256, content_type, byte_size, width, height,
                has_thumbnail, capture, original_name, caption, source_label, source_message_id, received_at, taken_at,
                has_location, perceptual_hash, revision, created_by, created_at, updated_at)
            VALUES ($1, $2, $3::uuid, $4, $5, $6, $7::int, $8::int, $9, $10, $11, $12, $13, $14, $15::timestamptz,
                $16::timestamptz, $17, $18::bigint, 1, $19, $20, $20)
            ON CONFLICT (organization_id, sha256) DO NOTHING
            RETURNING {MediaColumns}
            """, organizationId, Guid.NewGuid(), media.SupplierId, content.Sha256, content.ContentType, content.Bytes.Length,
            content.Width, content.Height, content.Thumbnail is not null, media.Capture, media.OriginalName, media.Caption,
            media.SourceLabel, media.SourceMessageId, media.ReceivedAt, content.TakenAt, content.HasLocation,
            content.PerceptualHash, actor, now);
        SourcingMedia? created = await ReadOptionalAsync(insert, ReadMedia, token);
        if (created is not null)
        {
            await transaction.CommitAsync(token);
            return new SourcingMediaUpload(created, false, []);
        }
        await using NpgsqlCommand existing = Command(connection, transaction,
            $"SELECT {MediaColumns} FROM sourcing.media WHERE organization_id=$1 AND sha256=$2", organizationId, content.Sha256);
        SourcingMedia duplicate = await ReadSingleAsync(existing, ReadMedia, token);
        await transaction.CommitAsync(token);
        return new SourcingMediaUpload(duplicate, true, []);
    }

    public async Task<SourcingMedia> UpdateMediaAsync(
        Guid organizationId, Guid mediaId, int expectedRevision, MediaUpdateInput input, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await RequireSupplierReferenceAsync(connection, null, organizationId, input.SupplierId, token);
        await using NpgsqlCommand update = Command(connection, null, $"""
            UPDATE sourcing.media SET supplier_id=$4::uuid, capture=$5, caption=$6, revision=revision+1, updated_at=$7
            WHERE organization_id=$1 AND id=$2 AND revision=$3
            RETURNING {MediaColumns}
            """, organizationId, mediaId, expectedRevision, input.SupplierId, input.Capture, input.Caption, time.GetUtcNow());
        return await ReadOptionalAsync(update, ReadMedia, token)
            ?? throw await MissingOrConflictAsync(connection, null, "media", organizationId, mediaId, "media item", token);
    }

    public async Task<int> AssignMediaAsync(
        Guid organizationId, IReadOnlyList<Guid> mediaIds, Guid? supplierId, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await RequireSupplierReferenceAsync(connection, null, organizationId, supplierId, token);
        await using NpgsqlCommand update = Command(connection, null, """
            UPDATE sourcing.media SET supplier_id=$3::uuid, revision=revision+1, updated_at=$4
            WHERE organization_id=$1 AND id = ANY($2) AND supplier_id IS DISTINCT FROM $3::uuid
            """, organizationId, mediaIds.ToArray(), supplierId, time.GetUtcNow());
        return await update.ExecuteNonQueryAsync(token);
    }

    public async Task DeleteMediaAsync(Guid organizationId, Guid mediaId, int expectedRevision, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        string sha256;
        await using (NpgsqlCommand delete = Command(connection, transaction,
            "DELETE FROM sourcing.media WHERE organization_id=$1 AND id=$2 AND revision=$3 RETURNING sha256",
            organizationId, mediaId, expectedRevision))
        {
            sha256 = await delete.ExecuteScalarAsync(token) as string
                ?? throw await MissingOrConflictAsync(connection, transaction, "media", organizationId, mediaId, "media item", token);
        }
        // SHA-256 is unique per organization, so the bytes are unreferenced once their media row is gone.
        await using (NpgsqlCommand bytes = Command(connection, transaction,
            "DELETE FROM sourcing.media_contents WHERE organization_id=$1 AND sha256=$2", organizationId, sha256))
        {
            await bytes.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    public async Task<SourcingMediaContent> GetMediaContentAsync(Guid organizationId, Guid mediaId, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null, """
            SELECT c.bytes, m.content_type, m.original_name
            FROM sourcing.media m
            JOIN sourcing.media_contents c ON c.organization_id=m.organization_id AND c.sha256=m.sha256
            WHERE m.organization_id=$1 AND m.id=$2
            """, organizationId, mediaId);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw NotFound("media item");
        return new SourcingMediaContent(reader.GetFieldValue<byte[]>(0), reader.GetString(1), reader.GetString(2));
    }

    public async Task<byte[]> GetMediaThumbnailAsync(Guid organizationId, Guid mediaId, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null, """
            SELECT c.thumbnail
            FROM sourcing.media m
            JOIN sourcing.media_contents c ON c.organization_id=m.organization_id AND c.sha256=m.sha256
            WHERE m.organization_id=$1 AND m.id=$2
            """, organizationId, mediaId);
        return await command.ExecuteScalarAsync(token) as byte[] ?? throw NotFound("thumbnail");
    }

    private static async Task<Supplier?> ReadSupplierAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid organizationId, Guid supplierId, CancellationToken token)
    {
        Guid id;
        string? key;
        string name, stage, notes;
        string? country, city, website;
        int revision;
        DateTimeOffset createdAt, updatedAt;
        await using (NpgsqlCommand command = Command(connection, transaction,
            $"SELECT {SupplierColumns} FROM sourcing.suppliers WHERE organization_id=$1 AND id=$2", organizationId, supplierId))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token))
        {
            if (!await reader.ReadAsync(token)) return null;
            id = reader.GetGuid(0);
            key = NullableString(reader, 1);
            name = reader.GetString(2);
            stage = reader.GetString(3);
            country = NullableString(reader, 4);
            city = NullableString(reader, 5);
            website = NullableString(reader, 6);
            notes = reader.GetString(7);
            revision = reader.GetInt32(8);
            createdAt = reader.GetFieldValue<DateTimeOffset>(9);
            updatedAt = reader.GetFieldValue<DateTimeOffset>(10);
        }
        var people = new List<SupplierPerson>();
        await using (NpgsqlCommand command = Command(connection, transaction, $"""
            SELECT {PersonColumns} FROM sourcing.people
            WHERE organization_id=$1 AND supplier_id=$2 ORDER BY lower(name), id
            """, organizationId, supplierId))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token)) people.Add(ReadPerson(reader));
        }
        var routes = new List<ContactRoute>();
        await using (NpgsqlCommand command = Command(connection, transaction, $"""
            SELECT {RouteColumns} FROM sourcing.contact_routes
            WHERE organization_id=$1 AND supplier_id=$2 ORDER BY is_primary DESC, channel, destination, id
            """, organizationId, supplierId))
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token)) routes.Add(ReadRoute(reader));
        }
        return new Supplier(id, key, name, stage, country, city, website, notes, people, routes, revision, createdAt, updatedAt);
    }

    private static SupplierPerson ReadPerson(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.GetString(2),
        NullableString(reader, 3),
        reader.GetString(4),
        reader.GetInt32(5),
        reader.GetFieldValue<DateTimeOffset>(6),
        reader.GetFieldValue<DateTimeOffset>(7));

    private static ContactRoute ReadRoute(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.GetGuid(1),
        reader.IsDBNull(2) ? null : reader.GetGuid(2),
        reader.GetString(3),
        reader.GetString(4),
        NullableString(reader, 5),
        NullableString(reader, 6),
        reader.GetBoolean(7),
        reader.GetBoolean(8),
        reader.GetString(9),
        reader.GetInt32(10),
        reader.GetFieldValue<DateTimeOffset>(11),
        reader.GetFieldValue<DateTimeOffset>(12));

    private static SourcingMedia ReadMedia(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        reader.IsDBNull(1) ? null : reader.GetGuid(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetInt32(4),
        reader.IsDBNull(5) ? null : reader.GetInt32(5),
        reader.IsDBNull(6) ? null : reader.GetInt32(6),
        reader.GetBoolean(7),
        reader.GetString(8),
        reader.GetString(9),
        reader.GetString(10),
        NullableString(reader, 11),
        NullableString(reader, 12),
        reader.IsDBNull(13) ? null : reader.GetFieldValue<DateTimeOffset>(13),
        reader.IsDBNull(14) ? null : reader.GetFieldValue<DateTimeOffset>(14),
        reader.GetBoolean(15),
        reader.GetInt32(16),
        reader.GetFieldValue<DateTimeOffset>(17),
        reader.GetFieldValue<DateTimeOffset>(18));

    private static async Task LockSupplierAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId, Guid supplierId, CancellationToken token)
    {
        await using NpgsqlCommand command = Command(connection, transaction,
            "SELECT id FROM sourcing.suppliers WHERE organization_id=$1 AND id=$2 FOR UPDATE", organizationId, supplierId);
        if (await command.ExecuteScalarAsync(token) is null) throw NotFound("supplier");
    }

    private static async Task RequirePersonAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId, Guid supplierId, Guid? personId,
        CancellationToken token)
    {
        if (personId is null) return;
        await using NpgsqlCommand command = Command(connection, transaction,
            "SELECT 1 FROM sourcing.people WHERE organization_id=$1 AND supplier_id=$2 AND id=$3", organizationId, supplierId, personId);
        if (await command.ExecuteScalarAsync(token) is null)
            throw new PreparationException("validation", "Choose a person who belongs to this supplier.");
    }

    private static async Task RequireSupplierReferenceAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid organizationId, Guid? supplierId, CancellationToken token)
    {
        if (supplierId is null) return;
        await using NpgsqlCommand command = Command(connection, transaction,
            "SELECT 1 FROM sourcing.suppliers WHERE organization_id=$1 AND id=$2", organizationId, supplierId);
        if (await command.ExecuteScalarAsync(token) is null)
            throw new PreparationException("validation", "Choose a supplier from this organization.");
    }

    private static async Task ClearPrimaryAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid organizationId, Guid supplierId, Guid? keptRouteId,
        DateTimeOffset now, CancellationToken token)
    {
        await using NpgsqlCommand command = Command(connection, transaction, """
            UPDATE sourcing.contact_routes SET is_primary=false, revision=revision+1, updated_at=$4
            WHERE organization_id=$1 AND supplier_id=$2 AND is_primary AND ($3::uuid IS NULL OR id<>$3::uuid)
            """, organizationId, supplierId, keptRouteId, now);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<PreparationException> MissingOrConflictAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string table, Guid organizationId, Guid id, string noun,
        CancellationToken token)
    {
        await using NpgsqlCommand command = Command(connection, transaction,
            $"SELECT 1 FROM sourcing.{table} WHERE organization_id=$1 AND id=$2", organizationId, id);
        return await command.ExecuteScalarAsync(token) is null ? NotFound(noun) : Conflict(noun);
    }

    private static async Task<T> ReadSingleAsync<T>(
        NpgsqlCommand command, Func<NpgsqlDataReader, T> read, CancellationToken token, string? duplicateMessage = null)
        where T : class
    {
        return await ReadOptionalAsync(command, read, token, duplicateMessage)
            ?? throw new InvalidOperationException("The statement returned no row.");
    }

    private static async Task<T?> ReadOptionalAsync<T>(
        NpgsqlCommand command, Func<NpgsqlDataReader, T> read, CancellationToken token, string? duplicateMessage = null)
        where T : class
    {
        try
        {
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? read(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation && duplicateMessage is not null)
        {
            throw new PreparationException("conflict", duplicateMessage);
        }
    }

    private static async Task ExecuteAsync(NpgsqlCommand command, string duplicateMessage, CancellationToken token)
    {
        try { await command.ExecuteNonQueryAsync(token); }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new PreparationException("conflict", duplicateMessage);
        }
    }

    private static string? NullableString(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static string EscapeLike(string value) => value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    private static PreparationException NotFound(string noun) => new("not-found", $"The {noun} was not found.");

    private static PreparationException Conflict(string noun) =>
        new("conflict", $"The {noun} changed since it was loaded. Reload it before saving again.");

    private static NpgsqlCommand Command(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (object? value in values)
        {
            if (value is null)
                command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Unknown, Value = DBNull.Value });
            else command.Parameters.AddWithValue(value);
        }
        return command;
    }
}
