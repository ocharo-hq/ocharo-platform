using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Application.Brand.Repositories;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="ConsumerGetAllQuery"/>; an app is current when it last read the latest release.</summary>
public sealed class ConsumerGetAllQueryHandler(IBrandRepository repository)
    : IQueryHandler<ConsumerGetAllQuery, AppResult<IReadOnlyList<ConsumerView>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<ConsumerView>>> HandleAsync(ConsumerGetAllQuery request, CancellationToken ct)
    {
        var latest = (await repository.FindLatestReleaseAsync(ct))?.Version ?? 0;
        var consumers = (await repository.GetConsumersAsync(ct)).OrderBy(c => c.App)
            .Select(c => new ConsumerView(c.App, c.LastSeenAt, c.LastRelease, c.LastRelease == latest, c.LastPath)).ToList();
        return AppResult<IReadOnlyList<ConsumerView>>.Ok(consumers);
    }
}
