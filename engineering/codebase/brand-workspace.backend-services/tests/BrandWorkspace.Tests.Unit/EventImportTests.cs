using AwesomeAssertions;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>Checks malformed event bundles are rejected before any repository access.</summary>
public sealed class EventImportTests
{
    [Theory]
    [InlineData("{\"format\":\"ocharo.events-import.v1\"}")]
    [InlineData("{\"format\":\"ocharo.events-import.v1\",\"events\":null}")]
    [InlineData("{\"format\":\"ocharo.events-import.v1\",\"events\":[null]}")]
    [InlineData("{\"format\":\"ocharo.events-import.v1\",\"events\":[{\"key\":\"first\",\"title\":\"Event\",\"scope\":\"local\",\"format\":\"market\"},null]}")]
    public async Task Import_ShouldRejectMalformedEventsBeforeRepositoryAccess(string json)
    {
        string file = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(file, json);
            var importer = new EventImportService(new UnusedRepository(), new EventValidationService());
            var owner = new OrganizationAccess(Guid.NewGuid(), "test-owner", "owner", true, true);
            Func<Task> act = () => importer.ImportAsync(owner, file, false, CancellationToken.None);
            PreparationException failure = (await act.Should().ThrowAsync<PreparationException>()).Which;
            failure.Code.Should().Be("validation");
            failure.Message.Should().Contain("nothing was imported");
        }
        finally { File.Delete(file); }
    }

    private sealed class UnusedRepository : IEventRepository
    {
        private static Exception Unexpected() => new InvalidOperationException("Invalid bundles must not reach persistence.");
        public Task<IReadOnlyList<ShowcaseEvent>> GetEventsAsync(Guid organizationId, ShowcaseEventFilter filter,
            int limit, int offset, CancellationToken token) => throw Unexpected();
        public Task<ShowcaseEventSummary> GetSummaryAsync(Guid organizationId, DateOnly today,
            int upcomingLimit, CancellationToken token) => throw Unexpected();
        public Task<ShowcaseEvent> GetEventAsync(Guid organizationId, Guid eventId, CancellationToken token) => throw Unexpected();
        public Task<ShowcaseEvent?> FindEventByKeyAsync(Guid organizationId, string key, CancellationToken token) => throw Unexpected();
        public Task<ShowcaseEvent> CreateEventAsync(Guid organizationId, string actor, string? key,
            ShowcaseEventInput input, CancellationToken token) => throw Unexpected();
        public Task<ShowcaseEvent> UpdateEventAsync(Guid organizationId, Guid eventId, int expectedRevision,
            ShowcaseEventInput input, CancellationToken token) => throw Unexpected();
        public Task DeleteEventAsync(Guid organizationId, Guid eventId, int expectedRevision, CancellationToken token) => throw Unexpected();
    }
}
