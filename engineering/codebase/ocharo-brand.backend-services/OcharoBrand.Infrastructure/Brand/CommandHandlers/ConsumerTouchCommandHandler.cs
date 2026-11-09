using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.CommandHandlers;

/// <summary>Handles <see cref="ConsumerTouchCommand"/>: a name of up to 40 letters, digits, <c>-</c> or <c>_</c> is recorded in
/// lowercase; any other is ignored.</summary>
public sealed class ConsumerTouchCommandHandler(IBrandRepository repository, TimeProvider time)
    : ICommandHandler<ConsumerTouchCommand, AppResult<Unit>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Unit>> HandleAsync(ConsumerTouchCommand request, CancellationToken ct)
    {
        var name = request.App?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 40 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            return AppResult<Unit>.Ok(Unit.Value);
        await repository.TouchConsumerAsync(new ConsumerRecord
        {
            App = name.ToLowerInvariant(), LastSeenAt = time.GetUtcNow(), LastRelease = request.Release, LastPath = request.Path,
        }, ct);
        return AppResult<Unit>.Ok(Unit.Value);
    }
}
