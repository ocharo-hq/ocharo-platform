using BrandWorkspace.Application.Files.Constants;

namespace BrandWorkspace.Application.Files.Models;

/// <summary>Names the Desk record that keeps a file alive: <c>{RecordType}:{RecordId}</c> is also the upload's idempotency key
/// and the manifest's reference.</summary>
public sealed record DeskFileReference(string RecordType, string RecordId)
{
    public static DeskFileReference PreparationMedia(Guid organizationId, Guid mediaId) =>
        new(DeskFiles.PreparationMedia, $"{organizationId:D}/{mediaId:D}");

    public static DeskFileReference PreparationExport(Guid organizationId, Guid exportId) =>
        new(DeskFiles.PreparationExport, $"{organizationId:D}/{exportId:D}");

    public static DeskFileReference CommerceMedia(Guid brandId, string sha256) =>
        new(DeskFiles.CommerceMedia, $"{brandId:D}/{sha256}");

    public static DeskFileReference SourcingMedia(Guid organizationId, string sha256) =>
        new(DeskFiles.SourcingMedia, $"{organizationId:D}/{sha256}");

    public override string ToString() => $"{RecordType}:{RecordId}";
}
