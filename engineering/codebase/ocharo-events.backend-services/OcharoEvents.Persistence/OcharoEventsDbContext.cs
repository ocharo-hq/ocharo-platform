using Microsoft.EntityFrameworkCore;

namespace OcharoEvents.Persistence;

/// <summary>EF Core context over schema <c>ocharo_events</c> of the product database (PostgreSQL). The service keeps no
/// event rows (WoW2 Events holds them), so it maps no table: it is the readiness probe's handle on the database, and the
/// key ring reads its one table through <see cref="PostgresXmlRepository"/>.</summary>
public sealed class OcharoEventsDbContext(DbContextOptions<OcharoEventsDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.HasDefaultSchema(EventsDatabaseConstants.Schema);
}
