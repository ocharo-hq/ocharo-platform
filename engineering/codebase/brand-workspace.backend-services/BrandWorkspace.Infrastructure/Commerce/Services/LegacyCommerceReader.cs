using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BrandWorkspace.Domain.Commerce;
using Dapper;
using Microsoft.Data.Sqlite;
using OcharoBrand.Domain.Brand;
using OcharoCatalogue.Domain.Catalogue.Models;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;

namespace BrandWorkspace.Infrastructure.Commerce.Services;

/// <summary>Explicit offline adoption of preserved pilot data. Source databases and media are never written.</summary>
public sealed class LegacyCommerceReader
{
    private const string BrandDatabase = "ocharo-brand.db";

    private const string CatalogueDatabase = "ocharo-catalogue.db";

    private static readonly JsonSerializerOptions Json = StoredJsonConstants.Default;

    public async Task<ImportedCommerce> ReadAsync(string codebaseRoot, string slug, CancellationToken token)
    {
        var brandRoot = Path.Combine(codebaseRoot, "ocharo-brand.backend-services", "OcharoBrand.Api", "App_Data");
        var catalogueRoot = Path.Combine(
            codebaseRoot,
            "ocharo-catalogue.backend-services",
            "OcharoCatalogue.Api",
            "App_Data");
        var evidence = new List<SourceEvidence>();
        var blobs = new List<CommerceBlob>();
        var brandRows = await RowsAsync(brandRoot, BrandDatabase, "SELECT Id,Revision,Json FROM drafts", token);
        CommerceRules.Require(brandRows.Count == 1, "A single preserved brand draft is required for this import.");
        var brand = JsonSerializer.Deserialize<BrandDraft>(brandRows[0].Json, Json)!;
        evidence.Add(new("brand-draft", brandRows[0].Id, brandRows[0].Revision, Sha(brandRows[0].Json)));
        var logoRows = await RowsAsync(
            brandRoot,
            BrandDatabase,
            "SELECT Id,0 AS Revision,Json FROM logo_versions",
            token);
        var versions = logoRows
            .Select(row => JsonSerializer.Deserialize<LogoVersion>(row.Json, Json)!)
            .ToDictionary(logo => logo.Id);
        var logos = brand.CurrentLogos.ToDictionary(logo => logo.Key, logo => versions[logo.Value]);
        var productRows = await RowsAsync(
            catalogueRoot,
            CatalogueDatabase,
            "SELECT Id,Revision,Json FROM products ORDER BY SortOrder,Id",
            token);
        var products = productRows
            .Select(row => new CommerceProduct(
                JsonSerializer.Deserialize<ProductDocument>(row.Json, Json)!,
                1,
                row.Revision))
            .ToArray();
        evidence.AddRange(productRows.Select(row =>
            new SourceEvidence("catalogue-product", row.Id, row.Revision, Sha(row.Json))));
        var collectionRows = await RowsAsync(
            catalogueRoot,
            CatalogueDatabase,
            "SELECT Id,Revision,Json FROM collections ORDER BY SortOrder,Id",
            token);
        evidence.AddRange(collectionRows.Select(row =>
            new SourceEvidence("catalogue-collection", row.Id, row.Revision, Sha(row.Json))));
        var hashes = logos.Values
            .Select(logo => logo.Sha256)
            .Concat(products.SelectMany(product => product.Document.Media).Select(media => media.Sha256))
            .Distinct()
            .ToHashSet();
        foreach (var root in new[] { brandRoot, catalogueRoot })
        {
            var database = root == brandRoot ? BrandDatabase : CatalogueDatabase;
            await WithDatabaseAsync(root, database, async connection =>
            {
                IEnumerable<BlobRow> stored = await connection.QueryAsync<BlobRow>(
                    new CommandDefinition(
                        "SELECT Sha256,ContentType,BlobPath FROM media_blobs",
                        cancellationToken: token));
                foreach (var row in stored)
                {
                    var hash = row.Sha256;
                    if (!hashes.Contains(hash) || blobs.Any(blob => blob.Sha256 == hash)) continue;
                    CommerceRules.Require(CommerceRules.Hash(hash), "Source media hash is invalid.");
                    var expected = $"media/{hash[..2]}/{hash}";
                    CommerceRules.Require(
                        row.BlobPath.Replace('\\', '/') == expected,
                        "Source blob path is not content-addressed.");
                    var path = Path.Combine(root, "blobs", row.BlobPath);
                    CommerceRules.Require(
                        File.Exists(path) && new FileInfo(path).Length <= 15 * 1024 * 1024,
                        "Source media is missing or too large.");
                    var bytes = await File.ReadAllBytesAsync(path, token);
                    CommerceRules.Require(
                        Convert.ToHexStringLower(SHA256.HashData(bytes)) == hash,
                        "Source media checksum mismatch.");
                    blobs.Add(new(hash, row.ContentType, bytes));
                }
                return true;
            }, token);
        }
        CommerceRules.Require(
            hashes.All(hash => blobs.Any(blob => blob.Sha256 == hash)),
            "Every referenced source image must exist before adoption.");
        var snapshot = new CommerceSnapshot
        {
            Slug = slug,
            Identity = brand.Identity,
            Logos = logos,
            Products = products,
            Collections = collectionRows
                .Select(row => JsonSerializer.Deserialize<CollectionDocument>(row.Json, Json)!)
                .ToArray(),
            MediaApprovals = hashes.ToDictionary(hash => hash, _ => new MediaApproval("unknown", false)),
            Sources = evidence
        };
        return new(snapshot, blobs);
    }

    private static string Sha(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>Reads one preserved table's id, revision and JSON document, in the query's order.</summary>
    private static Task<List<SourceRow>> RowsAsync(string root, string database, string sql, CancellationToken token)
    {
        return WithDatabaseAsync(root, database, async connection =>
        {
            IEnumerable<SourceRow> rows = await connection.QueryAsync<SourceRow>(
                new CommandDefinition(sql, cancellationToken: token));
            return rows.ToList();
        }, token);
    }

    /// <summary>Runs <paramref name="action"/> over a read-only temporary copy of a preserved database.</summary>
    private static async Task<T> WithDatabaseAsync<T>(
        string root,
        string name,
        Func<SqliteConnection, Task<T>> action,
        CancellationToken token)
    {
        var source = Path.Combine(root, name);
        CommerceRules.Require(
            File.Exists(source) && !File.Exists(source + "-wal"),
            "Import requires a preserved offline database without an active WAL.");
        var temporary = Path.Combine(Path.GetTempPath(), $"ocharo-adoption-{Guid.NewGuid():N}.db");
        try
        {
            File.Copy(source, temporary, false);
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = temporary,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(token);
            return await action(connection);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>One preserved record: its id, revision and stored JSON.</summary>
    private sealed class SourceRow
    {
        public string Id { get; init; } = string.Empty;
        public int Revision { get; init; }
        public string Json { get; init; } = string.Empty;
    }

    /// <summary>One preserved content-addressed blob and where its bytes lie.</summary>
    private sealed class BlobRow
    {
        public string Sha256 { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public string BlobPath { get; init; } = string.Empty;
    }
}
