using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Products.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.CommandHandlers;

/// <summary>Handles <see cref="ProductMediaAddCommand"/>.</summary>
public sealed class ProductMediaAddCommandHandler(
    ICatalogueRepository repository,
    IMediaStore media,
    ProductDetailService details,
    TimeProvider timeProvider) : ICommandHandler<ProductMediaAddCommand, AppResult<ProductDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ProductDetail>> HandleAsync(ProductMediaAddCommand request, CancellationToken ct)
    {
        if (!MediaRole.All.Contains(request.Role))
        {
            return AppResult<ProductDetail>.Fail(CatalogueErrors.Invalid("Choose a recognised image role."));
        }

        var record = await repository.FindProductAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<ProductDetail>.Fail(CatalogueErrors.NotFound("Product"));
        }

        if (request.ExpectedRevision is { } expected && expected != record.Revision)
        {
            var error = CatalogueErrors.Conflict(
                "The product changed since you opened it. Reload before adding images.");
            return AppResult<ProductDetail>.Fail(error);
        }

        var stored = await media.StoreAsync(request.Bytes, ct);
        var document = CatalogueJson.Read<ProductDocument>(record.Json);
        if (document.Media.Any(image => image.Sha256 == stored.Sha256))
        {
            return AppResult<ProductDetail>.Fail(
                CatalogueErrors.Conflict("This exact image is already attached to the product."));
        }

        var item = new ProductMedia(
            Guid.NewGuid().ToString("N")[..12],
            request.Role,
            SafeFileName(request.FileName),
            stored.ContentType,
            stored.ByteLength,
            stored.Sha256,
            stored.Width,
            stored.Height,
            MediaPreview.Exists(stored),
            null,
            null);
        var updated = document with { Media = [.. document.Media, item] };
        var next = ProductProjections.ToRecord(
            updated,
            record.Revision + 1,
            record.CreatedAt,
            timeProvider.GetUtcNow());
        var saved = await repository.UpdateProductAsync(
            next,
            record.Revision,
            $"Added {request.Role} image {item.FileName}",
            ct);
        return AppResult<ProductDetail>.Ok(await details.GetAsync(saved, ct));
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(name) || name.Length > 150 ? "image" : name;
    }
}
