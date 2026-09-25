using AwesomeAssertions;
using BrandWorkspace.Domain.Foundation.Models;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

public sealed class WorkerSnapshotTests
{
    [Theory]
    [InlineData(null, "waiting")]
    [InlineData(0, "healthy")]
    [InlineData(30, "healthy")]
    [InlineData(31, "stale")]
    [InlineData(-1, "stale")]
    public void Snapshot_ShouldClassifyFreshness_WhenHeartbeatIsObserved(int? ageSeconds, string expected)
    {
        var now = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset? seen = ageSeconds is { } age ? now.AddSeconds(-age) : null;
        var snapshot = WorkerSnapshot.From(seen, now);
        snapshot.State.Should().Be(expected);
        snapshot.LastSeenAtUtc.Should().Be(seen);
    }
}
