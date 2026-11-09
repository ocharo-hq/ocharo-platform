using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Mappers;
using OcharoBrand.Infrastructure.Brand.Serializers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.CommandHandlers;

/// <summary>Handles <see cref="AppearanceCancelCommand"/>.</summary>
public sealed class AppearanceCancelCommandHandler(IBrandRepository repository)
    : ICommandHandler<AppearanceCancelCommand, AppResult<BrandAppearance>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<BrandAppearance>> HandleAsync(AppearanceCancelCommand request, CancellationToken ct)
    {
        var record = await repository.FindAppearanceAsync(request.Id, ct);
        if (record is null)
            return AppResult<BrandAppearance>.Fail(BrandErrors.NotFound("Appearance"));
        var appearance = BrandJson.Read<BrandAppearance>(record.Json) with { Status = AppearanceStatus.Cancelled };
        await repository.SaveAppearanceAsync(BrandRecordMapper.ToRecord(appearance), ct);
        return AppResult<BrandAppearance>.Ok(appearance);
    }
}
