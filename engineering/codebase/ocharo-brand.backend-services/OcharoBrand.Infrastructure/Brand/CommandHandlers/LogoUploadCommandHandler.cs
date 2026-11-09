using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Application.Brand.Services;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Serializers;
using OcharoBrand.Infrastructure.Brand.Validators;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.CommandHandlers;

/// <summary>Handles <see cref="LogoUploadCommand"/>.</summary>
public sealed class LogoUploadCommandHandler(IBrandRepository repository, IMediaStore media, TimeProvider time)
    : ICommandHandler<LogoUploadCommand, AppResult<LogoVersion>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<LogoVersion>> HandleAsync(LogoUploadCommand request, CancellationToken ct)
    {
        if (!LogoSlots.All.Contains(request.Slot))
            return AppResult<LogoVersion>.Fail(BrandErrors.Invalid($"Unknown logo slot '{request.Slot}'."));
        if ((await media.StoreAsync(request.Bytes, ct)).IsFailure(out var refused, out var stored))
            return AppResult<LogoVersion>.Fail(refused);
        // The same file uploaded to the same slot again is the version it already is.
        var existing = (await repository.GetLogoVersionsAsync(ct)).Select(r => BrandJson.Read<LogoVersion>(r.Json))
            .FirstOrDefault(v => v.Slot == request.Slot && v.Sha256 == stored.Sha256);
        if (existing is not null)
            return AppResult<LogoVersion>.Ok(existing);
        if (!BrandInputValidator.TryClean(request.Note, 500, out var note))
            return AppResult<LogoVersion>.Fail(BrandErrors.TextTooLong(500));

        var version = new LogoVersion(Guid.NewGuid().ToString("N")[..12], request.Slot, stored.Sha256, SafeName(request.FileName), stored.ContentType,
            stored.ByteLength, stored.Width, stored.Height,
            // A file in WoW2 Files always has its preview: Files makes it as a variant.
            stored.PreviewPath is not null || stored.FileId is not null, request.Status, note, request.SourcePath, time.GetUtcNow());
        await repository.SaveLogoVersionAsync(new LogoVersionRecord
        {
            Id = version.Id, Slot = version.Slot, Sha256 = version.Sha256, UploadedAt = version.UploadedAt, Json = BrandJson.Write(version),
        }, ct);
        return AppResult<LogoVersion>.Ok(version);
    }

    /// <summary>The file name without any folder, or <c>logo</c> when nothing usable is left.</summary>
    private static string SafeName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(name) || name.Length > 150 ? "logo" : name;
    }
}
