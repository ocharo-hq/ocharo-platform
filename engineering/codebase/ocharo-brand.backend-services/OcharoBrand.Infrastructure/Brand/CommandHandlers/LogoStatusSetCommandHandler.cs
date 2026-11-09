using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Serializers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.CommandHandlers;

/// <summary>Handles <see cref="LogoStatusSetCommand"/>.</summary>
public sealed class LogoStatusSetCommandHandler(IBrandRepository repository)
    : ICommandHandler<LogoStatusSetCommand, AppResult<LogoVersion>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<LogoVersion>> HandleAsync(LogoStatusSetCommand request, CancellationToken ct)
    {
        if (!LogoStatus.All.Contains(request.Status))
            return AppResult<LogoVersion>.Fail(BrandErrors.Invalid("Unknown logo status."));
        var record = await repository.FindLogoVersionAsync(request.VersionId, ct);
        if (record is null)
            return AppResult<LogoVersion>.Fail(BrandErrors.NotFound("Logo version"));
        var version = BrandJson.Read<LogoVersion>(record.Json) with { Status = request.Status };
        record.Json = BrandJson.Write(version);
        await repository.SaveLogoVersionAsync(record, ct);
        return AppResult<LogoVersion>.Ok(version);
    }
}
