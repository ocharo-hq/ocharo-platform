using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoBrand.Application.Brand.Services;

/// <summary>Content-addressed image storage with a web preview per image.</summary>
public interface IMediaStore
{
    /// <summary>Stores an image once per SHA-256; refuses an empty, oversized or non-image upload.</summary>
    Task<Result<MediaBlobRecord>> StoreAsync(byte[] bytes, CancellationToken ct);

    /// <summary>Opens a stored file or its preview; not found when no such file is stored or its bytes are gone.</summary>
    Task<Result<MediaContent>> OpenAsync(string sha256, bool preview, CancellationToken ct);
}
