using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;

namespace BrandWorkspace.Infrastructure.Preparation.Services;

/// <summary>Packages exact stored bytes and copy without implying external publication.</summary>
public sealed class ManualPackageBuilder : IManualPackageBuilder
{
    public byte[] Build(PackageSnapshot snapshot)
    {
        if (snapshot.Media.Count > 10 || snapshot.Media.Sum(media => media.Asset.ByteLength) > 25L * 1024 * 1024)
            throw new PreparationException("too-large", "A package supports at most 10 images totalling 25 MiB.");
        if (snapshot.Draft.Approval?.Revision != snapshot.Draft.Revision
            || !snapshot.Draft.Media.SequenceEqual(snapshot.Media.Select(media => media.Asset)))
            throw new InvalidDataException("The package must contain the exact approved ordered media snapshot.");
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entries = new List<object>();
            foreach (var media in snapshot.Media)
            {
                if (media.Asset.OrganizationId != snapshot.OrganizationId || media.Bytes.LongLength != media.Asset.ByteLength
                    || Convert.ToHexStringLower(SHA256.HashData(media.Bytes)) != media.Asset.Sha256)
                    throw new InvalidDataException("Stored image identity or integrity does not match its snapshot.");
                var path = $"media/{media.Asset.Id:D}{(media.Asset.ContentType == "image/png" ? ".png" : ".jpg")}";
                Write(archive, path, media.Bytes, snapshot.Export.CreatedAtUtc);
                entries.Add(new { path, media.Asset.Id, media.Asset.FileName, media.Asset.ContentType,
                    media.Asset.ByteLength, media.Asset.Sha256, media.Asset.Provenance });
            }
            Write(archive, "caption.txt", Encoding.UTF8.GetBytes(snapshot.Draft.Caption), snapshot.Export.CreatedAtUtc);
            var manifest = new
            {
                schemaVersion = 1,
                snapshot.Export.PackageKind,
                snapshot.Export.MediaIncluded,
                snapshot.Export.PublicationStatus,
                snapshot.Export.Id,
                snapshot.Export.CreatedAtUtc,
                snapshot.OrganizationId,
                snapshot.Draft.BrandId,
                draftId = snapshot.Draft.Id,
                snapshot.Draft.Revision,
                snapshot.Draft.Approval,
                content = new { snapshot.Draft.Title, snapshot.Draft.Caption, snapshot.Draft.Language,
                    snapshot.Draft.Destination, snapshot.Draft.SourceNotes },
                media = entries,
                operatorReceipt = new { reportedBy = (string?)null, reportedPublishedAt = (string?)null,
                    destinationUrl = (string?)null, note = (string?)null }
            };
            Write(archive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, StoredJsonConstants.Default),
                snapshot.Export.CreatedAtUtc);
        }
        return output.ToArray();
    }

    private static void Write(ZipArchive archive, string path, byte[] bytes, DateTimeOffset createdAt)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
        entry.LastWriteTime = createdAt;
        using var stream = entry.Open();
        stream.Write(bytes);
    }
}
