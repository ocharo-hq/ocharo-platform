namespace BrandWorkspace.Application.Files.Constants;

/// <summary>The names Desk's files carry in WoW2 Files: the same app and references the migration manifest
/// (<c>engineering/deployment/files-migration.yml</c>, wave 3) gives the files it moves, so imports and new uploads share
/// one policy and the boot backfill finds both (planning/files-adoption.md #13, #20, #21).</summary>
public static class DeskFiles
{
    /// <summary>The app every Desk file belongs to: Desk's Identity client id.</summary>
    public const string AppId = "desk";

    /// <summary>The record type of an operator's preparation image; its record id is <c>{organization_id}/{id}</c>.</summary>
    public const string PreparationMedia = "desk.preparation-media";

    /// <summary>The record type of an approved revision's export ZIP; its record id is <c>{organization_id}/{id}</c>.</summary>
    public const string PreparationExport = "desk.preparation-export";

    /// <summary>The record type of a brand's commerce image or edition artifact; its record id is <c>{brand_id}/{sha256}</c>.</summary>
    public const string CommerceMedia = "desk.commerce-media";

    /// <summary>The record type of a supplier image, PDF or video; its record id is <c>{organization_id}/{sha256}</c>.</summary>
    public const string SourcingMedia = "desk.sourcing-media";

    /// <summary>How long a Files link stays valid.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    /// <summary>The width of a sourcing thumbnail, now a Files variant (planning/files-adoption.md #10).</summary>
    public const int SourcingThumbnailWidth = 480;

    /// <summary>The name a file carries in Files: its hash and the extension Files checks against its bytes.</summary>
    public static string Name(string sha256, string contentType) => $"{sha256[..16]}.{Extension(contentType)}";

    /// <summary>The file extension of a content type Desk stores.</summary>
    public static string Extension(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/webp" => "webp",
        "application/pdf" => "pdf",
        "video/mp4" => "mp4",
        "application/zip" => "zip",
        _ => "bin",
    };
}
