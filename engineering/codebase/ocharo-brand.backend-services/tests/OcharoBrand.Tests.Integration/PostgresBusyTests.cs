using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoBrand.Tests.Integration;

/// <summary>A write that loses to a concurrent one answers the conflict the editor retries, as SQLite's lock did.</summary>
public sealed class PostgresBusyTests
{
    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation)]
    [InlineData(PostgresErrorCodes.SerializationFailure)]
    [InlineData(PostgresErrorCodes.DeadlockDetected)]
    public async Task ExceptionMapper_ShouldMapALostWriteToAConflict(string sqlState)
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = BrandTestIdentity.Anonymous(host.Factory);
        var mapper = host.Factory.Services.GetRequiredService<IExceptionMapper>();

        var error = mapper.Map(new DbUpdateException("save failed", new PostgresException("lost", "ERROR", "ERROR", sqlState)));
        var stale = mapper.Map(new DbUpdateConcurrencyException("stale revision"));
        var other = mapper.Map(new PostgresException("syntax error", "ERROR", "ERROR", PostgresErrorCodes.SyntaxError));

        error.Type.Should().Be(AppErrorType.Conflict);
        error.Message.Should().Be("Another save is finishing. Reload and retry.");
        stale.Type.Should().Be(AppErrorType.Conflict);
        other.Type.Should().Be(AppErrorType.Unexpected);
    }
}
