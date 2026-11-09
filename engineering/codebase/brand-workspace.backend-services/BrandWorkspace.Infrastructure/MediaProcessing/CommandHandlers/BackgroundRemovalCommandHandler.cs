using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Application.MediaProcessing.Commands;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.MediaProcessing.CommandHandlers;

/// <summary>Handles <see cref="BackgroundRemovalCommand"/>.</summary>
public sealed class BackgroundRemovalCommandHandler(IBackgroundRemovalService worker)
    : ICommandHandler<BackgroundRemovalCommand, AppResult<BackgroundRemovalResult>>
{
    /// <summary>Holds the one image the worker processes at a time.</summary>
    private static readonly SemaphoreSlim Processing = new(1, 1);

    /// <inheritdoc />
    public async ValueTask<AppResult<BackgroundRemovalResult>> HandleAsync(
        BackgroundRemovalCommand request,
        CancellationToken cancellationToken)
    {
        if (!await Processing.WaitAsync(0, cancellationToken))
            return AppResult<BackgroundRemovalResult>.Fail(
                DeskErrors.Busy("Image processing is busy. Try again shortly."));
        try
        {
            var input = RasterImageGuard.Validate(request.Bytes);
            if (input.ContentType != request.ContentType)
                return AppResult<BackgroundRemovalResult>.Fail(
                    DeskErrors.UnsupportedMediaType("Image bytes do not match the declared type."));
            return AppResult<BackgroundRemovalResult>.Ok(await worker.RemoveAsync(request.Bytes, cancellationToken));
        }
        catch (BackgroundRemovalException exception)
        {
            return AppResult<BackgroundRemovalResult>.Fail(exception.Code == "invalid_image"
                ? DeskErrors.Invalid(exception.Message)
                : DeskErrors.Unavailable(exception.Message));
        }
        finally
        {
            Processing.Release();
        }
    }
}
