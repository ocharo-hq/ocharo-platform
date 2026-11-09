using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>Sends catalogue commands and queries through the host's mediator, each in its own scope as a request would;
/// a failure throws <see cref="CatalogueRejection"/>.</summary>
public sealed class CatalogueClient(IServiceProvider services)
{
    public async Task<T> SendAsync<T>(IRequest<AppResult<T>> request, CancellationToken ct = default) where T : notnull
    {
        await using var scope = services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(request, ct);
        return result.Match(success => success.Data, failure => throw new CatalogueRejection(failure.Error));
    }
}
