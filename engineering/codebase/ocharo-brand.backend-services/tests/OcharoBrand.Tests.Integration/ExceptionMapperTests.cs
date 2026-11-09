using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoBrand.Tests.Integration;

/// <summary>A save that meets SQLite's lock answers the conflict the editor retries, as the retired Brand middleware did.</summary>
public sealed class SqliteBusyTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public async Task ExceptionMapper_ShouldMapSqliteBusyAndLockedToAConflict(int code)
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = BrandTestIdentity.Anonymous(host.Factory);
        var mapper = host.Factory.Services.GetRequiredService<IExceptionMapper>();

        var error = mapper.Map(new SqliteException("database is locked", code));
        var other = mapper.Map(new SqliteException("constraint failed", 19));

        error.Type.Should().Be(AppErrorType.Conflict);
        error.Message.Should().Be("Another save is finishing. Reload and retry.");
        other.Type.Should().Be(AppErrorType.Unexpected);
    }
}
