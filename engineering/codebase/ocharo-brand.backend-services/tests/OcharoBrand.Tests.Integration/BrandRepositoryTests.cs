using Microsoft.Extensions.DependencyInjection;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Tests.Integration;

/// <summary>The brand repository over PostgreSQL, resolved from the real host: concurrent writes never fail, one draft save
/// wins its revision, and an instant keeps its moment whatever offset it was given.</summary>
public sealed class BrandRepositoryTests : IAsyncDisposable
{
    private readonly BrandHost host = BrandHost.Create(new Dictionary<string, string?>());

    public async ValueTask DisposeAsync() => await host.DisposeAsync();

    [Fact]
    public async Task SaveDraftAsync_ShouldLetOneConcurrentSaveWin_AndAnswerTheOthersWithAConflict()
    {
        using var boot = BrandTestIdentity.Anonymous(host.Factory);
        var current = await Repository().FindDraftAsync(default);

        var saves = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(() => Repository().SaveDraftAsync(
            new DraftRecord { Id = current!.Id, Revision = current.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow, Json = current.Json },
            current.Revision, $"Concurrent save {index}", default))));

        saves.Count(save => save.IsSuccess).Should().Be(1);
        (await Repository().FindDraftAsync(default))!.Revision.Should().Be(current!.Revision + 1);
        (await Repository().GetDraftRevisionsAsync(default)).Should().ContainSingle(revision => revision.Revision == current.Revision + 1);
    }

    [Fact]
    public async Task TouchConsumerAsync_ShouldKeepOneRowPerApp_WhenFirstReadsRace()
    {
        using var boot = BrandTestIdentity.Anonymous(host.Factory);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(() => Repository().TouchConsumerAsync(
            new ConsumerRecord { App = "catalogue", LastSeenAt = DateTimeOffset.UtcNow, LastRelease = index, LastPath = "/api/public/brand" }, default))));

        (await Repository().GetConsumersAsync(default)).Should().ContainSingle(consumer => consumer.App == "catalogue");
    }

    [Fact]
    public async Task SaveAppearanceAsync_ShouldKeepTheInstant_WhenGivenAnotherOffset()
    {
        using var boot = BrandTestIdentity.Anonymous(host.Factory);
        var starts = new DateTimeOffset(2027, 3, 21, 0, 0, 0, TimeSpan.FromHours(5));

        await Repository().SaveAppearanceAsync(new AppearanceRecord
        {
            Id = "navruz", StartsAt = starts, EndsAt = starts.AddDays(2), Status = AppearanceStatus.Scheduled, Json = "{}",
        }, default);
        var stored = await Repository().FindAppearanceAsync("navruz", default);

        stored!.StartsAt.Should().Be(starts);
        stored.StartsAt.Offset.Should().Be(TimeSpan.Zero);
    }

    /// <summary>A repository over its own scope and context, as one request sees it.</summary>
    private IBrandRepository Repository() =>
        host.Factory.Services.CreateScope().ServiceProvider.GetRequiredService<IBrandRepository>();
}
