using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using OcharoBrand.Infrastructure.Brand.Serializers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="ReleaseGetAllQuery"/>; each release lists what it changed from the one before.</summary>
public sealed class ReleaseGetAllQueryHandler(IBrandRepository repository)
    : IQueryHandler<ReleaseGetAllQuery, AppResult<IReadOnlyList<ReleaseSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<ReleaseSummary>>> HandleAsync(ReleaseGetAllQuery request, CancellationToken ct)
    {
        var releases = (await repository.GetReleasesAsync(ct)).OrderBy(r => r.Version).Select(r => BrandJson.Read<BrandRelease>(r.Json)).ToList();
        var summaries = releases.Select((r, i) => new ReleaseSummary(r.Version, r.PublishedAt, r.Note,
                i == 0
                    ? ["Birinchi nashr"]
                    : BrandDraftService.Differences(releases[i - 1],
                        new BrandDraft { Identity = r.Identity, CurrentLogos = r.Logos.ToDictionary(p => p.Key, p => p.Value.Id) }, r.Logos.Values.ToList())))
            .Reverse().ToList();
        return AppResult<IReadOnlyList<ReleaseSummary>>.Ok(summaries);
    }
}
