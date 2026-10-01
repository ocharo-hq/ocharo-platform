using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Commerce;
using Microsoft.Data.Sqlite;
using OcharoBrand.Domain.Brand;
using OcharoCatalogue.Domain.Catalogue.Models;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace BrandWorkspace.Infrastructure.Commerce;

/// <summary>Explicit offline adoption of preserved pilot data. Source databases and media are never written.</summary>
public sealed class LegacyCommerceReader
{
    private static readonly JsonSerializerOptions Json = StoredJsonConstants.Default;
    public async Task<ImportedCommerce> ReadAsync(string codebaseRoot, string slug, CancellationToken token)
    {
        var brandRoot=Path.Combine(codebaseRoot,"ocharo-brand.backend-services","OcharoBrand.Api","App_Data");
        var catalogueRoot=Path.Combine(codebaseRoot,"ocharo-catalogue.backend-services","OcharoCatalogue.Api","App_Data");
        var evidence=new List<SourceEvidence>();var blobs=new List<CommerceBlob>();
        var brandRows=await Rows(brandRoot,"ocharo-brand.db","SELECT Id,Revision,Json FROM drafts",token);
        CommerceRules.Require(brandRows.Count==1,"A single preserved brand draft is required for this import.");
        var brand=JsonSerializer.Deserialize<BrandDraft>(brandRows[0].Json,Json)!;
        evidence.Add(new("brand-draft",brandRows[0].Id,brandRows[0].Revision,Sha(brandRows[0].Json)));
        var logoRows=await Rows(brandRoot,"ocharo-brand.db","SELECT Id,0,Json FROM logo_versions",token);
        var versions=logoRows.Select(r=>JsonSerializer.Deserialize<LogoVersion>(r.Json,Json)!).ToDictionary(l=>l.Id);
        var logos=brand.CurrentLogos.ToDictionary(l=>l.Key,l=>versions[l.Value]);
        var productRows=await Rows(catalogueRoot,"ocharo-catalogue.db","SELECT Id,Revision,Json FROM products ORDER BY SortOrder,Id",token);
        var products=productRows.Select(row=>new CommerceProduct(JsonSerializer.Deserialize<ProductDocument>(row.Json,Json)!,1,row.Revision)).ToArray();
        evidence.AddRange(productRows.Select(row=>new SourceEvidence("catalogue-product",row.Id,row.Revision,Sha(row.Json))));
        var collectionRows=await Rows(catalogueRoot,"ocharo-catalogue.db","SELECT Id,Revision,Json FROM collections ORDER BY SortOrder,Id",token);
        evidence.AddRange(collectionRows.Select(row=>new SourceEvidence("catalogue-collection",row.Id,row.Revision,Sha(row.Json))));
        var hashes=logos.Values.Select(l=>l.Sha256).Concat(products.SelectMany(p=>p.Document.Media).Select(m=>m.Sha256)).Distinct().ToHashSet();
        foreach(var root in new[]{brandRoot,catalogueRoot})
        {
            var db=root==brandRoot?"ocharo-brand.db":"ocharo-catalogue.db";
            await WithDatabase(root,db,async connection=>{
                await using var cmd=connection.CreateCommand();cmd.CommandText="SELECT Sha256,ContentType,BlobPath FROM media_blobs";
                await using var reader=await cmd.ExecuteReaderAsync(token);
                while(await reader.ReadAsync(token))
                {
                    var hash=reader.GetString(0);if(!hashes.Contains(hash)||blobs.Any(b=>b.Sha256==hash))continue;
                    CommerceRules.Require(CommerceRules.Hash(hash),"Source media hash is invalid.");
                    var relative=reader.GetString(2);var expected=$"media/{hash[..2]}/{hash}";
                    CommerceRules.Require(relative.Replace('\\','/')==expected,"Source blob path is not content-addressed.");
                    var path=Path.Combine(root,"blobs",relative);
                    CommerceRules.Require(File.Exists(path)&&new FileInfo(path).Length<=15*1024*1024,"Source media is missing or too large.");
                    var bytes=await File.ReadAllBytesAsync(path,token);
                    CommerceRules.Require(Convert.ToHexStringLower(SHA256.HashData(bytes))==hash,"Source media checksum mismatch.");
                    blobs.Add(new(hash,reader.GetString(1),bytes));
                }
                return true;
            },token);
        }
        CommerceRules.Require(hashes.All(h=>blobs.Any(b=>b.Sha256==h)),"Every referenced source image must exist before adoption.");
        return new(new CommerceSnapshot {
            Slug=slug,Identity=brand.Identity,Logos=logos,Products=products,
            Collections=collectionRows.Select(r=>JsonSerializer.Deserialize<CollectionDocument>(r.Json,Json)!).ToArray(),
            MediaApprovals=hashes.ToDictionary(h=>h,_=>new MediaApproval("unknown",false)),Sources=evidence
        },blobs);
    }
    private static string Sha(string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record Row(string Id,int Revision,string Json);
    private static Task<List<Row>> Rows(string root,string db,string sql,CancellationToken token)=>WithDatabase(root,db,async connection=>{
        await using var cmd=connection.CreateCommand();cmd.CommandText=sql;await using var r=await cmd.ExecuteReaderAsync(token);var list=new List<Row>();
        while(await r.ReadAsync(token))list.Add(new(r.GetString(0),r.GetInt32(1),r.GetString(2)));return list;
    },token);
    private static async Task<T> WithDatabase<T>(string root,string name,Func<SqliteConnection,Task<T>> action,CancellationToken token)
    {
        var source=Path.Combine(root,name);
        CommerceRules.Require(File.Exists(source)&&!File.Exists(source+"-wal"),"Import requires a preserved offline database without an active WAL.");
        var temporary=Path.Combine(Path.GetTempPath(),$"ocharo-adoption-{Guid.NewGuid():N}.db");
        try {
            File.Copy(source,temporary,false);
            await using var connection=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=temporary,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());
            await connection.OpenAsync(token);return await action(connection);
        } finally { File.Delete(temporary); }
    }
}
