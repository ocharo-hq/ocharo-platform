using System.Security.Cryptography;
using System.Text.Json;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Commerce;
using Npgsql;
using NpgsqlTypes;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace BrandWorkspace.Persistence.Commerce;

/// <summary>Serialized immutable drafts/releases within the existing organization authority.</summary>
public sealed class CommerceRepository(NpgsqlDataSource source, TimeProvider time) : ICommerceRepository
{
    private static readonly JsonSerializerOptions Json = CommerceJson.Options;
    private static NpgsqlCommand Cmd(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values) { var parameter = new NpgsqlParameter { Value = value ?? DBNull.Value }; if (value is null) parameter.NpgsqlDbType = NpgsqlDbType.Unknown; command.Parameters.Add(parameter); }
        return command;
    }
    private static async Task Brand(NpgsqlConnection c, NpgsqlTransaction? tx, Guid org, Guid brand, bool locked, CancellationToken token)
    {
        await using var cmd = Cmd(c,tx,"SELECT id FROM preparation.brands WHERE organization_id=$1 AND id=$2" + (locked ? " FOR UPDATE" : ""),org,brand);
        if (await cmd.ExecuteScalarAsync(token) is null) throw new PreparationException("not-found","Brand unavailable.");
    }
    private static async Task<CommerceDraft?> Read(NpgsqlConnection c, NpgsqlTransaction? tx, Guid org, Guid brand, CancellationToken token)
    {
        await using var cmd = Cmd(c,tx,"SELECT p.current_revision,r.snapshot::text,p.updated_at FROM commerce.profiles p JOIN commerce.revisions r ON r.organization_id=p.organization_id AND r.brand_id=p.brand_id AND r.revision=p.current_revision WHERE p.organization_id=$1 AND p.brand_id=$2",org,brand);
        await using var reader = await cmd.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token) ? new(brand, reader.GetInt32(0),JsonSerializer.Deserialize<CommerceSnapshot>(reader.GetString(1),Json)!,reader.GetFieldValue<DateTimeOffset>(2)) : null;
    }
    public async Task<CommerceDraft?> GetAsync(Guid organizationId, Guid brandId, CancellationToken token)
    {
        await using var c=await source.OpenConnectionAsync(token); await Brand(c,null,organizationId,brandId,false,token);
        return await Read(c,null,organizationId,brandId,token);
    }
    public async Task<CommerceDraft> SaveAsync(Guid organizationId, string actor, Guid brandId, int expectedRevision, CommerceSnapshot snapshot, IReadOnlyList<CommerceBlob> blobs, bool owner, CancellationToken token)
    {
        CommerceRules.Require(snapshot is not null,"Brand identity is required.");
        // Generic edits cannot create, replace, approve, or erase server-owned artifacts.
        snapshot = snapshot with { CatalogueEditions = [] };
        CommerceRules.Validate(snapshot);
        await using var c=await source.OpenConnectionAsync(token); await using var tx=await c.BeginTransactionAsync(token);
        await Brand(c,tx,organizationId,brandId,true,token);
        var previous=await Read(c,tx,organizationId,brandId,token);
        if ((previous?.Revision ?? 0)!=expectedRevision) throw new PreparationException("conflict","The draft changed. Reload before saving.");
        await using(var slug=Cmd(c,tx,"SELECT public_slug FROM commerce.profiles WHERE organization_id=$1 AND brand_id=$2",organizationId,brandId))
            if(await slug.ExecuteScalarAsync(token) is string assigned && assigned!=snapshot.Slug) throw new PreparationException("conflict","The public slug is fixed after its first release.");
        snapshot = snapshot with { CatalogueEditions = previous?.Snapshot.CatalogueEditions ?? [], Products = snapshot.Products.Select(item => {
            var old = previous?.Snapshot.Products.FirstOrDefault(p => p.Document.Id == item.Document.Id);
            return item with { Translations = item.Translations ?? old?.Translations, MediaGroups = item.MediaGroups ?? old?.MediaGroups };
        }).ToArray() };
        if(!owner) snapshot=CommerceRules.RestrictEditorApprovals(snapshot,previous?.Snapshot);
        snapshot=snapshot with { Products=snapshot.Products.Select(p => {
            var old=previous?.Snapshot.Products.FirstOrDefault(x=>x.Document.Id==p.Document.Id);
            var same=old is not null && JsonSerializer.Serialize(new { old.Document, old.Translations, old.MediaGroups },Json)==JsonSerializer.Serialize(new { p.Document, p.Translations, p.MediaGroups },Json);
            return p with { Revision=old is null ? 1 : same ? old.Revision : checked(old.Revision+1), SourceRevision=old?.SourceRevision ?? p.SourceRevision };
        }).ToArray() };
        var result = await WriteDraft(c, tx, organizationId, actor, brandId, expectedRevision, snapshot, blobs, token);
        await tx.CommitAsync(token); return result;
    }
    private async Task<CommerceDraft> WriteDraft(NpgsqlConnection c, NpgsqlTransaction tx, Guid organizationId, string actor,
        Guid brandId, int expectedRevision, CommerceSnapshot snapshot, IReadOnlyList<CommerceBlob> blobs, CancellationToken token, int maximumBlobBytes = 15*1024*1024)
    {
        CommerceRules.Validate(snapshot);
        var revision=checked(expectedRevision+1);var now=time.GetUtcNow();
        await using(var cmd=Cmd(c,tx,"INSERT INTO commerce.profiles(organization_id,brand_id,current_revision,updated_at) VALUES($1,$2,$3,$4) ON CONFLICT(organization_id,brand_id) DO UPDATE SET current_revision=$3,updated_at=$4",organizationId,brandId,revision,now)) await cmd.ExecuteNonQueryAsync(token);
        foreach(var blob in blobs)
        {
            CommerceRules.Require(blob.Bytes.Length > 0 && blob.Bytes.Length <= maximumBlobBytes && blob.Sha256==Convert.ToHexStringLower(SHA256.HashData(blob.Bytes)),"Imported media bytes do not match their hash.");
            await using var cmd=Cmd(c,tx,"INSERT INTO commerce.media(brand_id,sha256,content_type,bytes) VALUES($1,$2,$3,$4) ON CONFLICT DO NOTHING",brandId,blob.Sha256,blob.ContentType,blob.Bytes); await cmd.ExecuteNonQueryAsync(token);
        }
        await using(var cmd=Cmd(c,tx,"INSERT INTO commerce.revisions(organization_id,brand_id,revision,snapshot,actor,created_at) VALUES($1,$2,$3,$4::jsonb,$5,$6)",organizationId,brandId,revision,JsonSerializer.Serialize(snapshot,Json),actor,now)) await cmd.ExecuteNonQueryAsync(token);
        return new(brandId,revision,snapshot,now);
    }
    public async Task<CommerceDraft> AttachCatalogueEditionAsync(Guid organizationId, string actor, Guid brandId, int expectedRevision,
        CatalogueEdition edition, IReadOnlyList<CommerceBlob> blobs, CancellationToken token)
    {
        CommerceRules.Require(!edition.ApprovedForPublication, "New catalogue editions require explicit owner approval.");
        await using var c=await source.OpenConnectionAsync(token); await using var tx=await c.BeginTransactionAsync(token);
        await Brand(c,tx,organizationId,brandId,true,token);
        var draft=await Read(c,tx,organizationId,brandId,token) ?? throw new PreparationException("not-found","Save the brand draft first.");
        if(draft.Revision!=expectedRevision) throw new PreparationException("conflict","The draft changed. Reload before attaching the edition.");
        CommerceRules.Require(edition.Pages.SelectMany(p=>p.ProductIds).All(id=>draft.Snapshot.Products.Any(p=>p.Document.Id==id)), "Catalogue pages must refer to products in this brand.");
        var expectedHashes=EditionHashes(edition).ToHashSet();
        CommerceRules.Require(blobs.Select(b=>b.Sha256).ToHashSet().SetEquals(expectedHashes),"Provide every catalogue artifact and no extra files.");
        var existing=draft.Snapshot.CatalogueEditions.FirstOrDefault(e=>e.Locale==edition.Locale);
        if(existing is not null && existing.SourceDesignId==edition.SourceDesignId && existing.SourceRevision==edition.SourceRevision &&
            existing.PdfSha256==edition.PdfSha256 && JsonSerializer.Serialize(existing.Pages,Json)==JsonSerializer.Serialize(edition.Pages,Json))
            return draft;
        var snapshot=draft.Snapshot with {CatalogueEditions=draft.Snapshot.CatalogueEditions.Where(e=>e.Locale!=edition.Locale).Append(edition).ToArray()};
        var result=await WriteDraft(c,tx,organizationId,actor,brandId,expectedRevision,snapshot,blobs,token,64*1024*1024);
        await tx.CommitAsync(token);return result;
    }
    private static IEnumerable<string> EditionHashes(CatalogueEdition e) => new[]{e.PdfSha256}.Concat(e.Pages.SelectMany(p=>new[]{p.PngSha256,p.PdfSha256}));
    public async Task<CommerceRelease> PublishCatalogueEditionAsync(Guid organizationId,string actor,Guid brandId,int expectedRevision,string locale,string note,CancellationToken token)
    {
        await using var c=await source.OpenConnectionAsync(token);await using var tx=await c.BeginTransactionAsync(token);
        await Brand(c,tx,organizationId,brandId,true,token);
        var draft=await Read(c,tx,organizationId,brandId,token)??throw new PreparationException("not-found","Save the brand draft first.");
        if(draft.Revision!=expectedRevision)throw new PreparationException("conflict","The draft changed. Review the latest revision.");
        var edition=draft.Snapshot.CatalogueEditions.SingleOrDefault(e=>e.Locale==locale)??throw new PreparationException("not-found","Attach a saved catalogue edition for this language first.");
        if(!edition.ApprovedForPublication)
            draft=await WriteDraft(c,tx,organizationId,actor,brandId,expectedRevision,draft.Snapshot with {
                CatalogueEditions=draft.Snapshot.CatalogueEditions.Select(e=>e.Id==edition.Id?e with {ApprovedForPublication=true}:e).ToArray()
            },[],token);
        var release=await WriteRelease(c,tx,organizationId,actor,brandId,draft,note,token);
        await tx.CommitAsync(token);return release;
    }
    private static CommerceRelease ReadRelease(NpgsqlDataReader r) => new(r.GetGuid(0),r.GetGuid(1),r.GetInt32(2),r.GetInt32(3),r.GetFieldValue<DateTimeOffset>(4),r.GetString(5),JsonSerializer.Deserialize<CommerceSnapshot>(r.GetString(6),Json)!,r.IsDBNull(7)?null:r.GetFieldValue<DateTimeOffset>(7));
    private const string ReleaseColumns="id,brand_id,revision,source_revision,published_at,note,snapshot::text,withdrawn_at";
    public async Task<IReadOnlyList<CommerceRelease>> ReleasesAsync(Guid organizationId,Guid brandId,CancellationToken token)
    {
        await using var c=await source.OpenConnectionAsync(token);await Brand(c,null,organizationId,brandId,false,token);
        await using var cmd=Cmd(c,null,$"SELECT {ReleaseColumns} FROM commerce.releases WHERE organization_id=$1 AND brand_id=$2 ORDER BY revision DESC",organizationId,brandId);
        await using var r=await cmd.ExecuteReaderAsync(token);var result=new List<CommerceRelease>();while(await r.ReadAsync(token))result.Add(ReadRelease(r));return result;
    }
    public async Task<CommerceRelease> PublishAsync(Guid organizationId,string actor,Guid brandId,int expectedRevision,string note,CancellationToken token)
    {
        CommerceRules.Require(!string.IsNullOrWhiteSpace(note)&&note.Length<=1000,"Record the reason for publishing (maximum 1000 characters).");
        await using var c=await source.OpenConnectionAsync(token);await using var tx=await c.BeginTransactionAsync(token);await Brand(c,tx,organizationId,brandId,true,token);
        var draft=await Read(c,tx,organizationId,brandId,token) ?? throw new PreparationException("not-found","Save the brand draft first.");
        if(draft.Revision!=expectedRevision)throw new PreparationException("conflict","The draft changed. Review the latest revision.");
        var release=await WriteRelease(c,tx,organizationId,actor,brandId,draft,note,token);
        await tx.CommitAsync(token);return release;
    }
    private async Task<CommerceRelease> WriteRelease(NpgsqlConnection c,NpgsqlTransaction tx,Guid organizationId,string actor,Guid brandId,CommerceDraft draft,string note,CancellationToken token)
    {
        CommerceRules.Require(!string.IsNullOrWhiteSpace(note)&&note.Length<=1000,"Record the reason for publishing (maximum 1000 characters).");
        var expectedRevision=draft.Revision;
        CommerceRules.Validate(draft.Snapshot);
        foreach(var hash in CommerceRules.ApprovedHashes(draft.Snapshot).Concat(draft.Snapshot.CatalogueEditions.Where(e=>e.ApprovedForPublication).SelectMany(EditionHashes)).Distinct())
        {
            await using var media=Cmd(c,tx,"SELECT 1 FROM commerce.media WHERE brand_id=$1 AND sha256=$2",brandId,hash);
            if(await media.ExecuteScalarAsync(token) is null)throw new PreparationException("validation","Approved image bytes are missing; attach the image before publishing.");
        }
        await using(var duplicate=Cmd(c,tx,"SELECT 1 FROM commerce.releases WHERE organization_id=$1 AND brand_id=$2 AND source_revision=$3 AND withdrawn_at IS NULL",organizationId,brandId,expectedRevision))
            if(await duplicate.ExecuteScalarAsync(token) is not null)throw new PreparationException("conflict","This draft revision already has an active release.");
        await using var version=Cmd(c,tx,"SELECT COALESCE(MAX(revision),0)+1 FROM commerce.releases WHERE organization_id=$1 AND brand_id=$2",organizationId,brandId);
        var revision=Convert.ToInt32(await version.ExecuteScalarAsync(token));var release=new CommerceRelease(Guid.NewGuid(),brandId,revision,expectedRevision,time.GetUtcNow(),note.Trim(),draft.Snapshot);
        try {
            await using(var head=Cmd(c,tx,"UPDATE commerce.profiles SET public_slug=$3,active_release_id=$4 WHERE organization_id=$1 AND brand_id=$2",organizationId,brandId,draft.Snapshot.Slug,release.Id)) await head.ExecuteNonQueryAsync(token);
        }catch(PostgresException ex)when(ex.SqlState==PostgresErrorCodes.UniqueViolation){throw new PreparationException("conflict","That public brand slug is already in use.");}
        await using(var cmd=Cmd(c,tx,"INSERT INTO commerce.releases(id,organization_id,brand_id,revision,source_revision,snapshot,note,actor,published_at) VALUES($1,$2,$3,$4,$5,$6::jsonb,$7,$8,$9)",release.Id,organizationId,brandId,revision,expectedRevision,JsonSerializer.Serialize(draft.Snapshot,Json),release.Note,actor,release.PublishedAt)) await cmd.ExecuteNonQueryAsync(token);
        return release;
    }
    public async Task WithdrawAsync(Guid organizationId,string actor,Guid brandId,Guid releaseId,string reason,CancellationToken token)
    {
        CommerceRules.Require(!string.IsNullOrWhiteSpace(reason)&&reason.Length<=1000,"A withdrawal reason is required.");
        await using var c=await source.OpenConnectionAsync(token);await using var tx=await c.BeginTransactionAsync(token);await Brand(c,tx,organizationId,brandId,true,token);
        await using(var cmd=Cmd(c,tx,"UPDATE commerce.releases SET withdrawn_at=$4,withdrawn_by=$5,withdrawal_reason=$6 WHERE organization_id=$1 AND brand_id=$2 AND id=$3 AND withdrawn_at IS NULL",organizationId,brandId,releaseId,time.GetUtcNow(),actor,reason))
            if(await cmd.ExecuteNonQueryAsync(token)!=1)throw new PreparationException("not-found","Active release unavailable.");
        await using(var cmd=Cmd(c,tx,"UPDATE commerce.profiles SET active_release_id=NULL WHERE organization_id=$1 AND brand_id=$2 AND active_release_id=$3",organizationId,brandId,releaseId))await cmd.ExecuteNonQueryAsync(token);
        await tx.CommitAsync(token);
    }
    public async Task<CommerceRelease?> PublicAsync(string slug,Guid? releaseId,CancellationToken token)
    {
        await using var c=await source.OpenConnectionAsync(token);
        await using var cmd=Cmd(c,null,$"SELECT {string.Join(",",ReleaseColumns.Split(',').Select(x=>"r."+x))} FROM commerce.releases r JOIN commerce.profiles p ON p.organization_id=r.organization_id AND p.brand_id=r.brand_id WHERE p.public_slug=$1 AND r.id=COALESCE($2::uuid,p.active_release_id) AND r.withdrawn_at IS NULL",slug,releaseId);
        await using var r=await cmd.ExecuteReaderAsync(token);return await r.ReadAsync(token)?ReadRelease(r):null;
    }
    public async Task<CommerceBlob?> BlobAsync(Guid brandId,string sha256,CancellationToken token)
    {
        await using var c=await source.OpenConnectionAsync(token);await using var cmd=Cmd(c,null,"SELECT content_type,bytes FROM commerce.media WHERE brand_id=$1 AND sha256=$2",brandId,sha256);
        await using var r=await cmd.ExecuteReaderAsync(token);return await r.ReadAsync(token)?new(sha256,r.GetString(0),(byte[])r[1]):null;
    }
    public async Task<CreativeImport> ImportCreativeAsync(Guid organizationId,string actor,CreativeImportInput input,CancellationToken token)
    {
        await using var c=await source.OpenConnectionAsync(token);await Brand(c,null,organizationId,input.BrandId,false,token);
        var result=new CreativeImport(Guid.NewGuid(),time.GetUtcNow(),"unreviewed",input);
        await using var cmd=Cmd(c,null,"INSERT INTO commerce.creative_imports(organization_id,id,brand_id,actor,created_at,snapshot) VALUES($1,$2,$3,$4,$5,$6::jsonb)",organizationId,result.Id,input.BrandId,actor,result.CreatedAt,JsonSerializer.Serialize(input,Json));await cmd.ExecuteNonQueryAsync(token);return result;
    }
    public async Task<IReadOnlyList<CreativeImport>> CreativeImportsAsync(Guid organizationId,Guid brandId,CancellationToken token)
    {
        await using var c=await source.OpenConnectionAsync(token);await Brand(c,null,organizationId,brandId,false,token);
        await using var cmd=Cmd(c,null,"SELECT id,created_at,snapshot::text FROM commerce.creative_imports WHERE organization_id=$1 AND brand_id=$2 ORDER BY created_at DESC LIMIT 100",organizationId,brandId);
        await using var r=await cmd.ExecuteReaderAsync(token);var list=new List<CreativeImport>();while(await r.ReadAsync(token))list.Add(new(r.GetGuid(0),r.GetFieldValue<DateTimeOffset>(1),"unreviewed",JsonSerializer.Deserialize<CreativeImportInput>(r.GetString(2),Json)!));return list;
    }
}
