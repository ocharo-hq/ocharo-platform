using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Models;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Domain.Commerce.Models;
using BrandWorkspace.Infrastructure.Commerce.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="CreativeImportCreateCommand"/>.</summary>
public sealed class CreativeImportCreateCommandHandler(
    ICommerceRepository repository,
    IPreparationRepository preparation)
    : ICommandHandler<CreativeImportCreateCommand, AppResult<CreativeImport>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CreativeImport>> HandleAsync(
        CreativeImportCreateCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.Access.CanEdit)
            return AppResult<CreativeImport>.Fail(DeskErrors.Forbidden("Editor access is required."));
        var input = request.Input;
        var organizationId = request.Access.OrganizationId;
        CommerceRules.ValidateCreativeInput(input);
        Result<PreparationMediaFile> derivative = await preparation.GetMediaFileAsync(
            organizationId,
            input.Derivative.MediaId,
            cancellationToken);
        if (derivative.IsFailure(out AppError? error, out var media)) return AppResult<CreativeImport>.Fail(error);
        if (media.Asset.Sha256 != input.Derivative.Sha256 || media.Asset.ContentType != input.Derivative.MediaType)
        {
            return AppResult<CreativeImport>.Fail(
                DeskErrors.Invalid("Derivative bytes do not match the submitted manifest."));
        }
        if (input.ProductId is not null)
        {
            Result<CommerceDraftLookup> lookup = await repository.GetAsync(
                organizationId,
                input.BrandId,
                cancellationToken);
            if (lookup.IsFailure(out AppError? missing, out var found)) return AppResult<CreativeImport>.Fail(missing);
            if (found.Draft?.Snapshot.Products.Any(product => product.Document.Id == input.ProductId) != true)
                return AppResult<CreativeImport>.Fail(DeskErrors.Invalid("Select a product in this brand."));
        }
        Result<CreativeImport> created = await repository.ImportCreativeAsync(
            organizationId,
            request.Access.UserId,
            input,
            cancellationToken);
        return created.ToAppResult();
    }
}
