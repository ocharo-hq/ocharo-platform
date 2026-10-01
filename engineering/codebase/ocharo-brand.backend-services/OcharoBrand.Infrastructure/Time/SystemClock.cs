using OcharoBrand.Application.Abstractions;

namespace OcharoBrand.Infrastructure.Time;

/// <summary>Wall-clock implementation of <see cref="IClock"/>.</summary>
internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
