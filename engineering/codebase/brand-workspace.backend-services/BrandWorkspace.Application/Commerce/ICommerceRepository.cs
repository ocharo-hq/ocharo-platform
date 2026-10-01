using BrandWorkspace.Domain.Commerce;
namespace BrandWorkspace.Application.Commerce;

public interface ICommerceRepository
{
    Task<CommerceDraft?> GetAsync(Guid organizationId, Guid brandId, CancellationToken token);
    Task<CommerceDraft> SaveAsync(Guid organizationId, string actor, Guid brandId, int expectedRevision, CommerceSnapshot snapshot, IReadOnlyList<CommerceBlob> blobs, bool owner, CancellationToken token);
    Task<IReadOnlyList<CommerceRelease>> ReleasesAsync(Guid organizationId, Guid brandId, CancellationToken token);
    Task<CommerceRelease> PublishAsync(Guid organizationId, string actor, Guid brandId, int expectedRevision, string note, CancellationToken token);
    Task<CommerceDraft> AttachCatalogueEditionAsync(Guid organizationId, string actor, Guid brandId, int expectedRevision, CatalogueEdition edition, IReadOnlyList<CommerceBlob> blobs, CancellationToken token);
    Task<CommerceRelease> PublishCatalogueEditionAsync(Guid organizationId, string actor, Guid brandId, int expectedRevision, string locale, string note, CancellationToken token);
    Task WithdrawAsync(Guid organizationId, string actor, Guid brandId, Guid releaseId, string reason, CancellationToken token);
    Task<CommerceRelease?> PublicAsync(string slug, Guid? releaseId, CancellationToken token);
    Task<CommerceBlob?> BlobAsync(Guid brandId, string sha256, CancellationToken token);
    Task<CreativeImport> ImportCreativeAsync(Guid organizationId, string actor, CreativeImportInput input, CancellationToken token);
    Task<IReadOnlyList<CreativeImport>> CreativeImportsAsync(Guid organizationId, Guid brandId, CancellationToken token);
}
