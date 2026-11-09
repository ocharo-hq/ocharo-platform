using BrandWorkspace.Application.Commerce.Models;
using BrandWorkspace.Domain.Commerce;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Application.Commerce.Repositories;

/// <summary>Persists brands' serialized commerce drafts and immutable releases within their organization.</summary>
public interface ICommerceRepository
{
    /// <summary>Reads the brand's draft, none before its first save; not found when the brand is unavailable.</summary>
    Task<Result<CommerceDraftLookup>> GetAsync(Guid organizationId, Guid brandId, CancellationToken token);

    /// <summary>
    /// Saves the next draft revision; invalid, not found, or a conflict on a stale revision or fixed slug.
    /// </summary>
    Task<Result<CommerceDraft>> SaveAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        CommerceSnapshot snapshot,
        IReadOnlyList<CommerceBlob> blobs,
        bool owner,
        CancellationToken token,
        bool preserveImportedPrices = false);

    /// <summary>Lists the brand's releases, newest first; not found when the brand is unavailable.</summary>
    Task<Result<IReadOnlyList<CommerceRelease>>> ReleasesAsync(
        Guid organizationId,
        Guid brandId,
        CancellationToken token);

    /// <summary>
    /// Publishes the draft revision; invalid, not found, or a conflict on a stale revision or taken slug.
    /// </summary>
    Task<Result<CommerceRelease>> PublishAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        string note,
        CancellationToken token);

    /// <summary>Attaches a catalogue edition and its artifacts as the next draft revision.</summary>
    Task<Result<CommerceDraft>> AttachCatalogueEditionAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        CatalogueEdition edition,
        IReadOnlyList<CommerceBlob> blobs,
        CancellationToken token);

    /// <summary>
    /// Publishes the draft with its unnamed edition for <paramref name="locale"/>, approving it first.
    /// </summary>
    Task<Result<CommerceRelease>> PublishCatalogueEditionAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        string locale,
        string note,
        CancellationToken token);

    /// <summary>Publishes the draft with the edition <paramref name="editionId"/>, approving it first.</summary>
    Task<Result<CommerceRelease>> PublishCatalogueEditionByIdAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        Guid editionId,
        string note,
        CancellationToken token);

    /// <summary>Withdraws an active release; invalid without a reason, not found when none is active.</summary>
    Task<Result> WithdrawAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        Guid releaseId,
        string reason,
        CancellationToken token);

    Task<CommerceRelease?> PublicAsync(string slug, Guid? releaseId, CancellationToken token);

    Task<CommerceBlob?> BlobAsync(Guid brandId, string sha256, CancellationToken token);

    /// <summary>
    /// Reads the WoW2 Files file holding a brand's media; none when the brand lacks it or PostgreSQL alone holds it.
    /// </summary>
    Task<string?> MediaFileAsync(Guid brandId, string sha256, CancellationToken token);

    /// <summary>Records a creative import; not found when the brand is unavailable.</summary>
    Task<Result<CreativeImport>> ImportCreativeAsync(
        Guid organizationId,
        string actor,
        CreativeImportInput input,
        CancellationToken token);

    /// <summary>Lists the brand's latest creative imports; not found when the brand is unavailable.</summary>
    Task<Result<IReadOnlyList<CreativeImport>>> CreativeImportsAsync(
        Guid organizationId,
        Guid brandId,
        CancellationToken token);
}
