using BrandWorkspace.Application.Preparation.Commands;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.CommandHandlers;

/// <summary>Handles <see cref="BrandCreateCommand"/>.</summary>
public sealed class BrandCreateCommandHandler(PreparationService service)
    : ICommandHandler<BrandCreateCommand, AppResult<Brand>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Brand>> HandleAsync(
        BrandCreateCommand request,
        CancellationToken cancellationToken)
    {
        return AppResult<Brand>.Ok(await service.CreateBrandAsync(
            request.Access,
            request.Name,
            request.TimeZone,
            cancellationToken));
    }
}
