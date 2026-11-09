using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Persistence;

/// <summary>EF Core context over schema <c>brand</c> of the product database (PostgreSQL), mapped onto the tables the
/// <c>brand</c> migration set creates; the host registers it with snake_case names.</summary>
public sealed class OcharoBrandDbContext(DbContextOptions<OcharoBrandDbContext> options) : DbContext(options)
{
    public DbSet<DraftRecord> Drafts => Set<DraftRecord>();
    public DbSet<DraftRevisionRecord> DraftRevisions => Set<DraftRevisionRecord>();
    public DbSet<LogoVersionRecord> LogoVersions => Set<LogoVersionRecord>();
    public DbSet<ReleaseRecord> Releases => Set<ReleaseRecord>();
    public DbSet<AppearanceRecord> Appearances => Set<AppearanceRecord>();
    public DbSet<MediaBlobRecord> MediaBlobs => Set<MediaBlobRecord>();
    public DbSet<ConsumerRecord> Consumers => Set<ConsumerRecord>();

    /// <summary>Stores every instant as UTC: <c>timestamptz</c> keeps the instant, never the offset, and Npgsql refuses any
    /// other offset.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(BrandDatabaseConstants.Schema);
        modelBuilder.Entity<DraftRecord>(e => { e.ToTable("drafts"); e.HasKey(x => x.Id); e.Property(x => x.Revision).IsConcurrencyToken(); });
        modelBuilder.Entity<DraftRevisionRecord>(e => { e.ToTable("draft_revisions"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever(); });
        modelBuilder.Entity<LogoVersionRecord>(e => { e.ToTable("logo_versions"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<ReleaseRecord>(e => { e.ToTable("releases"); e.HasKey(x => x.Version); e.Property(x => x.Version).ValueGeneratedNever(); });
        modelBuilder.Entity<AppearanceRecord>(e => { e.ToTable("appearances"); e.HasKey(x => x.Id); });
        modelBuilder.Entity<MediaBlobRecord>(e => { e.ToTable("media_blobs"); e.HasKey(x => x.Sha256); });
        modelBuilder.Entity<ConsumerRecord>(e => { e.ToTable("consumers"); e.HasKey(x => x.App); });
    }

    /// <summary>Converts an instant to UTC on its way into the store.</summary>
    private sealed class UtcDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(
        value => value.ToUniversalTime(), value => value.ToUniversalTime());
}
