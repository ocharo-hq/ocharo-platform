using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="CreativeImportCreateCommand"/>.</summary>
public sealed class CreativeImportCreateCommandHandler(ICommerceRepository repository, IPreparationRepository preparation)
    : ICommandHandler<CreativeImportCreateCommand, AppResult<CreativeImport>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CreativeImport>> HandleAsync(CreativeImportCreateCommand request, CancellationToken cancellationToken)
    {
        if (!request.Access.CanEdit)
            return AppResult<CreativeImport>.Fail(DeskErrors.Forbidden("Editor access is required."));
        var input = request.Input;
        var organizationId = request.Access.OrganizationId;
        CommerceRules.ValidateCreativeInput(input);
        var media = await preparation.GetMediaFileAsync(organizationId, input.Derivative.MediaId, cancellationToken);
        if (media.Asset.Sha256 != input.Derivative.Sha256 || media.Asset.ContentType != input.Derivative.MediaType)
            return AppResult<CreativeImport>.Fail(DeskErrors.Invalid("Derivative bytes do not match the submitted manifest."));
        if (input.ProductId is not null)
        {
            var draft = await repository.GetAsync(organizationId, input.BrandId, cancellationToken);
            if (draft?.Snapshot.Products.Any(product => product.Document.Id == input.ProductId) != true)
                return AppResult<CreativeImport>.Fail(DeskErrors.Invalid("Select a product in this brand."));
        }
        return AppResult<CreativeImport>.Ok(await repository.ImportCreativeAsync(organizationId, request.Access.UserId, input, cancellationToken));
    }
}
