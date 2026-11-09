namespace OcharoBrand.Application.Brand.Constants;

/// <summary>Holds the Brand failure codes a problem names in its <c>brandCode</c> extension, beside the SDK's <c>code</c>.</summary>
public static class BrandErrorCodes
{
    /// <summary>The error metadata key and problem extension that carry the code.</summary>
    public const string Key = "brandCode";

    /// <summary>An upload that is empty or over the size limit.</summary>
    public const string ImageTooLarge = "image_too_large";

    /// <summary>An upload that is not a PNG, JPEG or WebP image.</summary>
    public const string ImageTypeUnsupported = "image_type_unsupported";

    /// <summary>A WoW2 Files call refused for a reason Brand has no closer kind for.</summary>
    public const string FilesRefused = "files_refused";

    /// <summary>A private API request without a session.</summary>
    public const string SignInRequired = "sign_in_required";

    /// <summary>A session whose active organization is not the one Brand serves, or has no role there.</summary>
    public const string OrganizationAccessRequired = "organization_access_required";

    /// <summary>An unsafe request without <c>brand:edit</c>.</summary>
    public const string EditPermissionRequired = "edit_permission_required";

    /// <summary>A publication without <c>brand:approve</c>.</summary>
    public const string ApprovalPermissionRequired = "approval_permission_required";

    /// <summary>A session route reached over plain HTTP.</summary>
    public const string HttpsRequired = "https_required";

    /// <summary>A brand route reached while the retired SQLite store waits for <c>import-sqlite</c>.</summary>
    public const string StorageMoving = "storage_moving";
}
